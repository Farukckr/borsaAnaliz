using BorsaAnaliz.Web.Models;
using BorsaAnaliz.Web.Services;
using BorsaAnaliz.Web.ViewModels;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace BorsaAnaliz.Web.Controllers;

public sealed class StocksController : Controller
{
    private static readonly SemaphoreSlim AiCooldownGate = new(1, 1);

    private static readonly HashSet<string> AllowedRanges = new(StringComparer.OrdinalIgnoreCase)
    {
        "1mo", "3mo", "1y", "5y"
    };

    private static readonly HashSet<string> AllowedIntervals = new(StringComparer.OrdinalIgnoreCase)
    {
        "1d", "1h", "1wk"
    };

    private readonly IStockCatalogService _stockCatalog;
    private readonly IMarketDataService _marketData;
    private readonly IAiCommentaryService _aiCommentary;
    private readonly IMemoryCache _cache;

    public StocksController(
        IStockCatalogService stockCatalog,
        IMarketDataService marketData,
        IAiCommentaryService aiCommentary,
        IMemoryCache cache)
    {
        _stockCatalog = stockCatalog;
        _marketData = marketData;
        _aiCommentary = aiCommentary;
        _cache = cache;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var symbols = await _stockCatalog.GetSymbolsAsync(cancellationToken);
        var quotes = await _marketData.GetQuotesAsync(
            symbols.Select(stock => stock.Symbol),
            cancellationToken);
        var stocks = symbols
            .Select(stock => new StockListItemViewModel(
                stock,
                quotes.GetValueOrDefault(stock.Symbol)))
            .ToArray();

        return View(new StocksIndexViewModel(stocks));
    }

    [HttpGet("/Stocks/Details/{symbol}")]
    public async Task<IActionResult> Details(string symbol, CancellationToken cancellationToken)
    {
        var stock = await FindStockAsync(symbol, cancellationToken);
        if (stock is null)
        {
            return NotFound();
        }

        var quote = await _marketData.GetQuoteAsync(stock.Symbol, cancellationToken);
        return View(new StockDetailsViewModel(stock, quote));
    }

    [HttpGet("/api/stocks/{symbol}/history")]
    public async Task<IActionResult> History(
        string symbol,
        [FromQuery] string range = "1y",
        [FromQuery] string interval = "1d",
        CancellationToken cancellationToken = default)
    {
        var stock = await FindStockAsync(symbol, cancellationToken);
        if (stock is null)
        {
            return NotFound(new { message = "Hisse bulunamadı." });
        }

        if (!AllowedRanges.Contains(range) || !AllowedIntervals.Contains(interval))
        {
            return BadRequest(new { message = "Desteklenmeyen tarih aralığı veya veri sıklığı." });
        }

        var candles = await _marketData.GetHistoryAsync(
            stock.Symbol,
            range.ToLowerInvariant(),
            interval.ToLowerInvariant(),
            cancellationToken);
        var response = candles.Select(candle => new CandleResponse(
            candle.Time.ToUnixTimeSeconds(),
            candle.Open,
            candle.High,
            candle.Low,
            candle.Close,
            candle.Volume));

        return Ok(response);
    }

    [Authorize]
    [HttpPost("/api/stocks/{symbol}/ai-comment")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AiComment(
        string symbol,
        CancellationToken cancellationToken = default)
    {
        var stock = await FindStockAsync(symbol, cancellationToken);
        if (stock is null)
        {
            return NotFound(new { message = "Hisse bulunamadı." });
        }

        var responseCacheKey = $"ai-commentary:{stock.Symbol.ToUpperInvariant()}";
        if (_cache.TryGetValue(responseCacheKey, out AiCommentaryCacheEntry? cachedCommentary) &&
            cachedCommentary is not null)
        {
            return Ok(new AiCommentaryResponse(
                cachedCommentary.Commentary,
                true,
                cachedCommentary.GeneratedAt,
                true));
        }

        if (!_aiCommentary.IsConfigured)
        {
            var unconfigured = await _aiCommentary.GetCommentaryAsync(
                stock.Symbol,
                Array.Empty<Candle>(),
                cancellationToken);
            return Ok(new AiCommentaryResponse(
                unconfigured.Commentary,
                false,
                DateTimeOffset.UtcNow,
                unconfigured.Succeeded));
        }

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Challenge();
        }

        var candles = await _marketData.GetHistoryAsync(
            stock.Symbol,
            "1y",
            "1d",
            cancellationToken);
        if (candles.Count == 0)
        {
            var unavailable = await _aiCommentary.GetCommentaryAsync(
                stock.Symbol,
                candles,
                cancellationToken);
            return Ok(new AiCommentaryResponse(
                unavailable.Commentary,
                false,
                DateTimeOffset.UtcNow,
                unavailable.Succeeded));
        }

        var cooldownKey = $"ai-commentary-cooldown:{userId}";
        await AiCooldownGate.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(cooldownKey, out DateTimeOffset nextAllowed) &&
                nextAllowed > DateTimeOffset.UtcNow)
            {
                var retryAfter = Math.Max(1, (int)Math.Ceiling((nextAllowed - DateTimeOffset.UtcNow).TotalSeconds));
                Response.Headers["Retry-After"] = retryAfter.ToString(CultureInfo.InvariantCulture);
                return StatusCode(
                    StatusCodes.Status429TooManyRequests,
                    new
                    {
                        message = $"Yeni bir AI yorumu için {retryAfter} saniye bekleyin.",
                        retryAfterSeconds = retryAfter
                    });
            }

            var cooldownUntil = DateTimeOffset.UtcNow.AddSeconds(30);
            _cache.Set(cooldownKey, cooldownUntil, cooldownUntil);
        }
        finally
        {
            AiCooldownGate.Release();
        }

        var result = await _aiCommentary.GetCommentaryAsync(
            stock.Symbol,
            candles,
            cancellationToken);
        var generatedAt = DateTimeOffset.UtcNow;
        if (result.Succeeded)
        {
            _cache.Set(
                responseCacheKey,
                new AiCommentaryCacheEntry(result.Commentary, generatedAt),
                TimeSpan.FromMinutes(5));
        }

        return Ok(new AiCommentaryResponse(
            result.Commentary,
            false,
            generatedAt,
            result.Succeeded));
    }

    [HttpGet("/api/stocks/{symbol}/indicators")]
    public async Task<IActionResult> Indicators(
        string symbol,
        [FromQuery] string range = "1y",
        CancellationToken cancellationToken = default)
    {
        var stock = await FindStockAsync(symbol, cancellationToken);
        if (stock is null)
        {
            return NotFound(new { message = "Hisse bulunamadı." });
        }

        if (!AllowedRanges.Contains(range))
        {
            return BadRequest(new { message = "Desteklenmeyen tarih aralığı." });
        }

        var normalizedRange = range.ToLowerInvariant();
        var candles = await _marketData.GetHistoryAsync(
            stock.Symbol,
            normalizedRange,
            "1d",
            cancellationToken);
        var closes = candles.Select(candle => candle.Close).ToArray();
        var sma20 = IndicatorCalculator.Sma(closes, 20);
        var sma50 = IndicatorCalculator.Sma(closes, 50);
        var sma200 = IndicatorCalculator.Sma(closes, 200);
        var ema12 = IndicatorCalculator.Ema(closes, 12);
        var ema26 = IndicatorCalculator.Ema(closes, 26);
        var rsi14 = IndicatorCalculator.Rsi(closes);
        var macd = IndicatorCalculator.Macd(closes);
        var bollinger = IndicatorCalculator.Bollinger(closes);

        var latestMacd = LastValue(macd.Macd);
        var latestMacdSignal = LastValue(macd.Signal);
        var latestRsi = LastValue(rsi14);
        var response = new IndicatorsResponse(
            stock.Symbol,
            normalizedRange,
            new IndicatorSeriesResponse(
                ToPoints(candles, sma20),
                ToPoints(candles, sma50),
                ToPoints(candles, sma200),
                ToPoints(candles, ema12),
                ToPoints(candles, ema26),
                ToPoints(candles, rsi14),
                ToMacdPoints(candles, macd),
                ToBollingerPoints(candles, bollinger)),
            new IndicatorLatestResponse(
                LastValue(sma20),
                LastValue(sma50),
                LastValue(sma200),
                LastValue(ema12),
                LastValue(ema26),
                latestRsi,
                GetRsiSignal(latestRsi),
                latestMacd,
                latestMacdSignal,
                LastValue(macd.Histogram),
                GetMacdSignal(latestMacd, latestMacdSignal),
                LastValue(bollinger.Upper),
                LastValue(bollinger.Middle),
                LastValue(bollinger.Lower)));

        return Ok(response);
    }

    private async Task<StockSymbol?> FindStockAsync(string? symbol, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(symbol))
        {
            return null;
        }

        var symbols = await _stockCatalog.GetSymbolsAsync(cancellationToken);
        return symbols.FirstOrDefault(stock =>
            stock.Symbol.Equals(symbol, StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<IndicatorPoint> ToPoints(
        IReadOnlyList<Candle> candles,
        IReadOnlyList<decimal?> values)
    {
        var points = new List<IndicatorPoint>();
        for (var index = 0; index < Math.Min(candles.Count, values.Count); index++)
        {
            if (values[index] is decimal value)
            {
                points.Add(new IndicatorPoint(candles[index].Time.ToUnixTimeSeconds(), value));
            }
        }

        return points;
    }

    private static IReadOnlyList<MacdPoint> ToMacdPoints(
        IReadOnlyList<Candle> candles,
        MacdValues values)
    {
        var points = new List<MacdPoint>();
        for (var index = 0; index < Math.Min(candles.Count, values.Macd.Count); index++)
        {
            if (values.Macd[index] is decimal macd)
            {
                points.Add(new MacdPoint(
                    candles[index].Time.ToUnixTimeSeconds(),
                    macd,
                    values.Signal[index],
                    values.Histogram[index]));
            }
        }

        return points;
    }

    private static IReadOnlyList<BollingerPoint> ToBollingerPoints(
        IReadOnlyList<Candle> candles,
        BollingerValues values)
    {
        var points = new List<BollingerPoint>();
        for (var index = 0; index < Math.Min(candles.Count, values.Middle.Count); index++)
        {
            if (values.Middle[index] is decimal middle &&
                values.Upper[index] is decimal upper &&
                values.Lower[index] is decimal lower)
            {
                points.Add(new BollingerPoint(
                    candles[index].Time.ToUnixTimeSeconds(),
                    middle,
                    upper,
                    lower));
            }
        }

        return points;
    }

    private static decimal? LastValue(IReadOnlyList<decimal?> values)
    {
        for (var index = values.Count - 1; index >= 0; index--)
        {
            if (values[index] is decimal value)
            {
                return value;
            }
        }

        return null;
    }

    private static string GetRsiSignal(decimal? rsi)
    {
        return rsi switch
        {
            > 70m => "Aşırı alım",
            < 30m => "Aşırı satım",
            null => "Veri yok",
            _ => "Nötr"
        };
    }

    private static string GetMacdSignal(decimal? macd, decimal? signal)
    {
        if (macd is null || signal is null)
        {
            return "Veri yok";
        }

        return macd > signal ? "Pozitif" : macd < signal ? "Negatif" : "Nötr";
    }

    private sealed record AiCommentaryCacheEntry(string Commentary, DateTimeOffset GeneratedAt);
}
