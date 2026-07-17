using System.Globalization;
using System.Text.Json;
using BorsaAnaliz.Web.Models;
using Microsoft.Extensions.Caching.Memory;

namespace BorsaAnaliz.Web.Services;

public sealed class KapCompanyService : IKapCompanyService
{
    private const string OwnershipItemKey = "kpy41_acc5_sermayede_dogrudan";
    private const string SubsidiariesItemKey = "kpy41_acc7_bagli_ortakliklar";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);
    private static readonly CultureInfo TurkishCulture = CultureInfo.GetCultureInfo("tr-TR");

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<KapCompanyService> _logger;
    private readonly Lazy<Task<IReadOnlyDictionary<string, string>>> _members;

    public KapCompanyService(
        HttpClient httpClient,
        IMemoryCache cache,
        IWebHostEnvironment environment,
        ILogger<KapCompanyService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
        var path = Path.Combine(environment.ContentRootPath, "Data", "kap-members.json");
        _members = new Lazy<Task<IReadOnlyDictionary<string, string>>>(() => LoadMembersAsync(path));
    }

    public async Task<KapCompanyProfile?> GetCompanyProfileAsync(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var normalizedSymbol = symbol?.Trim().ToUpperInvariant() ?? string.Empty;
        if (!normalizedSymbol.EndsWith(".IS", StringComparison.Ordinal))
        {
            return null;
        }

        var cacheKey = $"kap-company-profile:{normalizedSymbol}";
        if (_cache.TryGetValue(cacheKey, out CacheEntry? cached))
        {
            return cached?.Profile;
        }

        KapCompanyProfile? profile = null;
        try
        {
            var members = await _members.Value.WaitAsync(cancellationToken);
            var memberOid = members.GetValueOrDefault(normalizedSymbol) ??
                await ResolveMemberOidAsync(normalizedSymbol, cancellationToken);
            if (memberOid is null)
            {
                _logger.LogInformation("{Symbol} için KAP üye eşlemesi bulunamadı.", normalizedSymbol);
            }
            else
            {
                var ownershipTask = FetchLatestValueSafeAsync(
                    memberOid,
                    OwnershipItemKey,
                    normalizedSymbol,
                    cancellationToken);
                var subsidiariesTask = FetchLatestValueSafeAsync(
                    memberOid,
                    SubsidiariesItemKey,
                    normalizedSymbol,
                    cancellationToken);
                await Task.WhenAll(ownershipTask, subsidiariesTask);

                var ownership = ParseOwnership(await ownershipTask);
                var subsidiaries = ParseSubsidiaries(await subsidiariesTask);
                if (ownership.Count > 0 || subsidiaries.Count > 0)
                {
                    profile = new KapCompanyProfile(ownership, subsidiaries);
                }
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("{Symbol} için KAP şirket bilgisi isteği zaman aşımına uğradı.", normalizedSymbol);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "{Symbol} için KAP şirket bilgileri alınamadı.", normalizedSymbol);
        }

        _cache.Set(cacheKey, new CacheEntry(profile), CacheDuration);
        return profile;
    }

    private async Task<string?> ResolveMemberOidAsync(
        string normalizedSymbol,
        CancellationToken cancellationToken)
    {
        var stockCode = normalizedSymbol[..^3];
        using var response = await _httpClient.GetAsync(
            $"tr/api/member/filter/{Uri.EscapeDataString(stockCode)}",
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("KAP üye arama yanıtı bir dizi değil.");
        }

        var matches = document.RootElement.EnumerateArray()
            .Select(item => GetString(item, "mkkMemberOid"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return matches.Length == 1 ? matches[0] : null;
    }

    private async Task<JsonElement?> FetchLatestValueSafeAsync(
        string memberOid,
        string itemKey,
        string symbol,
        CancellationToken cancellationToken)
    {
        try
        {
            return await FetchLatestValueAsync(memberOid, itemKey, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "{Symbol} için KAP şirket bilgi kalemi alınamadı: {ItemKey}",
                symbol,
                itemKey);
            return null;
        }
    }

    private async Task<JsonElement?> FetchLatestValueAsync(
        string memberOid,
        string itemKey,
        CancellationToken cancellationToken)
    {
        var path = $"tr/api/company-detail/get-history/{Uri.EscapeDataString(memberOid)}/{itemKey}/N";
        using var response = await _httpClient.GetAsync(path, cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("KAP şirket geçmişi yanıtı bir dizi değil.");
        }

        JsonElement? latestValue = null;
        DateTime latestDate = DateTime.MinValue;
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var date = ParseKapDate(GetString(item, "creationDate"));
            if (latestValue is null || date > latestDate)
            {
                latestValue = value.Clone();
                latestDate = date;
            }
        }

        return latestValue;
    }

    private static IReadOnlyList<KapOwnershipRow> ParseOwnership(JsonElement? value)
    {
        if (value is not { ValueKind: JsonValueKind.Array })
        {
            return [];
        }

        var rows = new List<KapOwnershipRow>();
        foreach (var row in value.Value.EnumerateArray())
        {
            var holder = GetString(row, "shareholder");
            if (string.IsNullOrWhiteSpace(holder) || IsTotal(holder) ||
                !TryGetDecimal(row, "ratioInCapital", out var percentage))
            {
                continue;
            }

            rows.Add(new KapOwnershipRow(holder, percentage));
        }

        return rows
            .OrderByDescending(row => row.SharePercentage)
            .ThenBy(row => row.Holder, StringComparer.Create(TurkishCulture, true))
            .ToArray();
    }

    private static IReadOnlyList<KapSubsidiary> ParseSubsidiaries(JsonElement? value)
    {
        if (value is not { ValueKind: JsonValueKind.Array })
        {
            return [];
        }

        var rows = new List<KapSubsidiary>();
        foreach (var row in value.Value.EnumerateArray())
        {
            var name = GetString(row, "companyTitle");
            var relation = GetString(row, "relationWithTheCompany");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(relation) ||
                TurkishCulture.CompareInfo.IndexOf(
                    relation,
                    "BAĞLI ORTAKLIK",
                    CompareOptions.IgnoreCase) < 0)
            {
                continue;
            }

            rows.Add(new KapSubsidiary(
                name,
                GetString(row, "scopeOfActivitiesOfCompany"),
                TryGetDecimal(row, "ratioOfCapitalShareOfCompany", out var percentage)
                    ? percentage
                    : null));
        }

        return rows
            .DistinctBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(row => row.Name, StringComparer.Create(TurkishCulture, true))
            .ToArray();
    }

    private async Task<IReadOnlyDictionary<string, string>> LoadMembersAsync(string path)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            using var document = await JsonDocument.ParseAsync(stream);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
            {
                throw new JsonException("KAP üye eşleme dosyası bir dizi değil.");
            }

            var members = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in document.RootElement.EnumerateArray())
            {
                var symbol = GetString(item, "symbol");
                var memberOid = GetString(item, "memberOid");
                if (!string.IsNullOrWhiteSpace(symbol) && !string.IsNullOrWhiteSpace(memberOid))
                {
                    members[symbol.ToUpperInvariant()] = memberOid;
                }
            }

            return members;
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "KAP üye eşleme dosyası yüklenemedi: {Path}", path);
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static bool IsTotal(string holder) =>
        holder.Equals("TOPLAM", StringComparison.CurrentCultureIgnoreCase) ||
        holder.Equals("TOTAL", StringComparison.OrdinalIgnoreCase);

    private static DateTime ParseKapDate(string? value)
    {
        var formats = new[] { "dd/MM/yyyy HH:mm:ss", "dd/MM/yyyy" };
        return DateTime.TryParseExact(
            value,
            formats,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var date)
            ? date
            : DateTime.MinValue;
    }

    private static bool TryGetDecimal(JsonElement element, string propertyName, out decimal value)
    {
        value = default;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number)
        {
            return property.TryGetDecimal(out value);
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = property.GetString();
        return decimal.TryParse(text, NumberStyles.Number, TurkishCulture, out value) ||
               decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out value);
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()?.Trim()
            : null;

    private sealed record CacheEntry(KapCompanyProfile? Profile);
}
