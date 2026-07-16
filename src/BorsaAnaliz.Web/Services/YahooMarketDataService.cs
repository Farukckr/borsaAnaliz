using System.Text.Json;
using BorsaAnaliz.Web.Models;
using Microsoft.Extensions.Caching.Memory;

namespace BorsaAnaliz.Web.Services;

public sealed class YahooMarketDataService : IMarketDataService
{
    private static readonly TimeSpan QuoteCacheDuration = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan HistoryCacheDuration = TimeSpan.FromHours(1);

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<YahooMarketDataService> _logger;

    public YahooMarketDataService(
        HttpClient httpClient,
        IMemoryCache cache,
        ILogger<YahooMarketDataService> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<Quote?> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        var cacheKey = $"quote:{normalizedSymbol}";

        if (_cache.TryGetValue<QuoteCacheEntry>(cacheKey, out var cached))
        {
            return cached?.Value;
        }

        Quote? quote;
        try
        {
            using var document = await GetChartDocumentAsync(
                normalizedSymbol,
                "1d",
                "1m",
                cancellationToken);
            quote = ParseQuote(document.RootElement, normalizedSymbol);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Yahoo fiyat isteği zaman aşımına uğradı: {Symbol}", normalizedSymbol);
            quote = null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Yahoo fiyat verisi alınamadı: {Symbol}", normalizedSymbol);
            quote = null;
        }

        _cache.Set(cacheKey, new QuoteCacheEntry(quote), QuoteCacheDuration);
        return quote;
    }

    public async Task<IReadOnlyDictionary<string, Quote?>> GetQuotesAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default)
    {
        var normalizedSymbols = symbols
            .Where(symbol => !string.IsNullOrWhiteSpace(symbol))
            .Select(NormalizeSymbol)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var quotes = new Dictionary<string, Quote?>(StringComparer.OrdinalIgnoreCase);

        foreach (var batch in normalizedSymbols.Chunk(20))
        {
            var batchResults = await Task.WhenAll(batch.Select(async symbol =>
                new KeyValuePair<string, Quote?>(
                    symbol,
                    await GetQuoteAsync(symbol, cancellationToken))));

            foreach (var result in batchResults)
            {
                quotes[result.Key] = result.Value;
            }
        }

        return quotes;
    }

    public async Task<IReadOnlyList<Candle>> GetHistoryAsync(
        string symbol,
        string range,
        string interval,
        CancellationToken cancellationToken = default)
    {
        var normalizedSymbol = NormalizeSymbol(symbol);
        var cacheKey = $"history:{normalizedSymbol}:{range}:{interval}";

        if (_cache.TryGetValue<HistoryCacheEntry>(cacheKey, out var cached))
        {
            return cached?.Value ?? [];
        }

        IReadOnlyList<Candle> candles;
        try
        {
            using var document = await GetChartDocumentAsync(
                normalizedSymbol,
                range,
                interval,
                cancellationToken);
            candles = ParseCandles(document.RootElement);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Yahoo geçmiş veri isteği zaman aşımına uğradı: {Symbol}", normalizedSymbol);
            candles = [];
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Yahoo geçmiş verisi alınamadı: {Symbol}", normalizedSymbol);
            candles = [];
        }

        _cache.Set(cacheKey, new HistoryCacheEntry(candles), HistoryCacheDuration);
        return candles;
    }

    private async Task<JsonDocument> GetChartDocumentAsync(
        string symbol,
        string range,
        string interval,
        CancellationToken cancellationToken)
    {
        var requestUri = $"v8/finance/chart/{Uri.EscapeDataString(symbol)}" +
            $"?range={Uri.EscapeDataString(range)}&interval={Uri.EscapeDataString(interval)}";
        using var response = await _httpClient.GetAsync(requestUri, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
    }

    private static Quote? ParseQuote(JsonElement root, string symbol)
    {
        var result = GetChartResult(root);
        var meta = result.GetProperty("meta");
        var price = ReadDecimal(meta, "regularMarketPrice");
        if (price is null)
        {
            return null;
        }

        var previousClose = ReadDecimal(meta, "chartPreviousClose") ?? ReadDecimal(meta, "previousClose");
        var change = previousClose is null ? null : price - previousClose;
        var changePercent = previousClose is null or 0 ? null : change / previousClose * 100;
        var currency = ReadString(meta, "currency") ?? (symbol.EndsWith(".IS", StringComparison.OrdinalIgnoreCase) ? "TRY" : "USD");
        var marketTimestamp = ReadLong(meta, "regularMarketTime");

        return new Quote(
            symbol,
            price.Value,
            previousClose,
            change,
            changePercent,
            currency,
            marketTimestamp is null ? null : DateTimeOffset.FromUnixTimeSeconds(marketTimestamp.Value));
    }

    private static IReadOnlyList<Candle> ParseCandles(JsonElement root)
    {
        var result = GetChartResult(root);
        if (!result.TryGetProperty("timestamp", out var timestamps) ||
            !result.TryGetProperty("indicators", out var indicators) ||
            !indicators.TryGetProperty("quote", out var quoteSets) ||
            quoteSets.ValueKind != JsonValueKind.Array ||
            quoteSets.GetArrayLength() == 0)
        {
            return [];
        }

        var quote = quoteSets[0];
        var opens = quote.GetProperty("open");
        var highs = quote.GetProperty("high");
        var lows = quote.GetProperty("low");
        var closes = quote.GetProperty("close");
        var volumes = quote.GetProperty("volume");
        var count = new[]
        {
            timestamps.GetArrayLength(),
            opens.GetArrayLength(),
            highs.GetArrayLength(),
            lows.GetArrayLength(),
            closes.GetArrayLength(),
            volumes.GetArrayLength()
        }.Min();
        var candles = new List<Candle>(count);

        for (var index = 0; index < count; index++)
        {
            var timestamp = ReadLong(timestamps[index]);
            var open = ReadDecimal(opens[index]);
            var high = ReadDecimal(highs[index]);
            var low = ReadDecimal(lows[index]);
            var close = ReadDecimal(closes[index]);
            if (timestamp is null || open is null || high is null || low is null || close is null)
            {
                continue;
            }

            candles.Add(new Candle(
                DateTimeOffset.FromUnixTimeSeconds(timestamp.Value),
                open.Value,
                high.Value,
                low.Value,
                close.Value,
                ReadLong(volumes[index]) ?? 0));
        }

        return candles;
    }

    private static JsonElement GetChartResult(JsonElement root)
    {
        if (!root.TryGetProperty("chart", out var chart) ||
            !chart.TryGetProperty("result", out var results) ||
            results.ValueKind != JsonValueKind.Array ||
            results.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Yahoo yanıtında grafik sonucu bulunamadı.");
        }

        return results[0];
    }

    private static decimal? ReadDecimal(JsonElement parent, string propertyName)
    {
        return parent.TryGetProperty(propertyName, out var property) ? ReadDecimal(property) : null;
    }

    private static decimal? ReadDecimal(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var result) ? result : null;
    }

    private static long? ReadLong(JsonElement parent, string propertyName)
    {
        return parent.TryGetProperty(propertyName, out var property) ? ReadLong(property) : null;
    }

    private static long? ReadLong(JsonElement value)
    {
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var result) ? result : null;
    }

    private static string? ReadString(JsonElement parent, string propertyName)
    {
        return parent.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static string NormalizeSymbol(string symbol)
    {
        return symbol.Trim().ToUpperInvariant();
    }

    private sealed record QuoteCacheEntry(Quote? Value);

    private sealed record HistoryCacheEntry(IReadOnlyList<Candle> Value);
}
