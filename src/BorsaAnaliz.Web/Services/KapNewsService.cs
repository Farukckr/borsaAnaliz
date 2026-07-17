using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using BorsaAnaliz.Web.Models;
using Microsoft.Extensions.Caching.Memory;

namespace BorsaAnaliz.Web.Services;

public sealed class KapNewsService : IKapNewsService
{
    private const string CacheKey = "kap-disclosures:latest";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan IstanbulOffset = TimeSpan.FromHours(3);

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly IStockCatalogService _stockCatalog;
    private readonly ILogger<KapNewsService> _logger;

    public KapNewsService(
        HttpClient httpClient,
        IMemoryCache cache,
        IStockCatalogService stockCatalog,
        ILogger<KapNewsService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _stockCatalog = stockCatalog;
        _logger = logger;
    }

    public async Task<IReadOnlyList<KapDisclosure>> GetLatestAsync(
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<KapDisclosure>? cached) &&
            cached is not null)
        {
            return cached;
        }

        IReadOnlyList<KapDisclosure> disclosures;
        try
        {
            disclosures = await FetchAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("KAP bildirim isteği zaman aşımına uğradı.");
            disclosures = [];
        }
        catch (Exception exception) when (exception is HttpRequestException or JsonException or FormatException)
        {
            _logger.LogWarning(exception, "KAP bildirimleri alınamadı veya ayrıştırılamadı.");
            disclosures = [];
        }

        _cache.Set(CacheKey, disclosures, CacheDuration);
        return disclosures;
    }

    private async Task<IReadOnlyList<KapDisclosure>> FetchAsync(CancellationToken cancellationToken)
    {
        var today = DateOnly.FromDateTime(DateTimeOffset.UtcNow.ToOffset(IstanbulOffset).Date);
        var payload = new
        {
            fromDate = today.AddDays(-2).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            toDate = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            memberType = string.Empty,
            mkkMemberOidList = Array.Empty<string>(),
            inactiveMkkMemberOidList = Array.Empty<string>(),
            disclosureClass = string.Empty,
            subjectList = Array.Empty<string>(),
            isLate = string.Empty,
            mainSector = string.Empty,
            sector = string.Empty,
            subSector = string.Empty,
            marketOid = string.Empty,
            index = string.Empty,
            bdkReview = string.Empty,
            bdkMemberOidList = Array.Empty<string>(),
            year = string.Empty,
            term = string.Empty,
            ruleType = string.Empty,
            period = string.Empty,
            fromSrc = false,
            srcCategory = string.Empty,
            disclosureIndexList = Array.Empty<string>()
        };

        using var response = await _httpClient.PostAsJsonAsync(
            "tr/api/disclosure/members/byCriteria",
            payload,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new JsonException("KAP yanıtının kök öğesi bir dizi değil.");
        }

        var catalog = await _stockCatalog.GetSymbolsAsync(cancellationToken);
        var catalogSymbols = catalog
            .Select(stock => stock.Symbol)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var disclosures = new List<KapDisclosure>();
        foreach (var element in document.RootElement.EnumerateArray())
        {
            if (TryParseDisclosure(element, catalogSymbols, out var disclosure))
            {
                disclosures.Add(disclosure);
            }
        }

        var result = disclosures
            .OrderByDescending(item => item.PublishedAt)
            .Take(100)
            .ToArray();
        _logger.LogInformation("KAP üzerinden {Count} güncel bildirim alındı.", result.Length);
        return result;
    }

    private static bool TryParseDisclosure(
        JsonElement element,
        IReadOnlySet<string> catalogSymbols,
        out KapDisclosure disclosure)
    {
        disclosure = default!;
        if (element.ValueKind != JsonValueKind.Object ||
            !TryGetInt64(element, "disclosureIndex", out var id) ||
            !TryGetPublishedAt(element, out var publishedAt))
        {
            return false;
        }

        var stockCodes = GetStockCodes(element);
        var matchedSymbol = stockCodes
            .Select(code => $"{code}.IS")
            .FirstOrDefault(catalogSymbols.Contains);
        var companyName = GetString(element, "kapTitle")
            ?? stockCodes.FirstOrDefault()
            ?? "KAP Bildirimi";
        var subject = GetString(element, "subject")
            ?? GetString(element, "summary")
            ?? "KAP Bildirimi";
        var disclosureClass = GetString(element, "disclosureClass") ?? "DİĞER";
        var disclosureType = GetString(element, "disclosureType") ?? disclosureClass;

        disclosure = new KapDisclosure(
            id,
            publishedAt,
            companyName,
            stockCodes,
            matchedSymbol,
            GetCategoryLabel(disclosureClass),
            disclosureType,
            subject,
            GetString(element, "summary"));
        return true;
    }

    private static IReadOnlyList<string> GetStockCodes(JsonElement element)
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        AddCodes(codes, element, "stockCodes");
        AddCodes(codes, element, "relatedStocks");
        return codes.ToArray();
    }

    private static void AddCodes(HashSet<string> codes, JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return;
        }

        if (property.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in property.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    AddCodeText(codes, item.GetString());
                }
            }
        }
        else if (property.ValueKind == JsonValueKind.String)
        {
            AddCodeText(codes, property.GetString());
        }
    }

    private static void AddCodeText(HashSet<string> codes, string? value)
    {
        foreach (var rawCode in (value ?? string.Empty).Split(
                     [',', ';'],
                     StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var code = rawCode.ToUpperInvariant();
            if (code.EndsWith(".E", StringComparison.OrdinalIgnoreCase))
            {
                code = code[..^2];
            }

            if (code.Length is > 0 and <= 16)
            {
                codes.Add(code);
            }
        }
    }

    private static bool TryGetPublishedAt(JsonElement element, out DateTimeOffset publishedAt)
    {
        publishedAt = default;
        var value = GetString(element, "publishDate");
        if (!DateTime.TryParseExact(
                value,
                "dd.MM.yyyy HH:mm:ss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateTime))
        {
            return false;
        }

        publishedAt = new DateTimeOffset(
            DateTime.SpecifyKind(dateTime, DateTimeKind.Unspecified),
            IstanbulOffset);
        return true;
    }

    private static bool TryGetInt64(JsonElement element, string propertyName, out long value)
    {
        value = default;
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return false;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number => property.TryGetInt64(out value),
            JsonValueKind.String => long.TryParse(
                property.GetString(),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out value),
            _ => false
        };
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) &&
               property.ValueKind == JsonValueKind.String
            ? property.GetString()?.Trim()
            : null;
    }

    private static string GetCategoryLabel(string value)
    {
        return value.ToUpperInvariant() switch
        {
            "ODA" => "Özel Durum",
            "FR" => "Finansal Rapor",
            "DKB" => "BIST Duyurusu",
            "DG" => "Diğer Bildirim",
            _ => value
        };
    }
}
