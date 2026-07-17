using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using BorsaAnaliz.Web.Models;
using Microsoft.Extensions.Options;

namespace BorsaAnaliz.Web.Services;

public sealed class GeminiCommentaryService : IAiCommentaryService
{
    public const string Disclaimer = "Bu bir yatırım tavsiyesi değildir.";
    public const string IncompleteResponseMessage = "AI yorumu tamamlanamadı, tekrar deneyin.";

    private const string SummaryHeading = "## Özet";

    private readonly HttpClient _httpClient;
    private readonly AiOptions _options;
    private readonly ILogger<GeminiCommentaryService> _logger;

    public GeminiCommentaryService(
        HttpClient httpClient,
        IOptions<AiOptions> options,
        ILogger<GeminiCommentaryService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        _options.Provider.Equals("Gemini", StringComparison.OrdinalIgnoreCase) &&
        !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<AiCommentaryResult> GetCommentaryAsync(
        string symbol,
        IReadOnlyList<Candle> candles,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            return Failure("AI yorumu henüz yapılandırılmamış. Yönetici Ai:ApiKey ayarını eklediğinde bu özellik kullanılabilir.");
        }

        if (candles.Count == 0)
        {
            return Failure("AI yorumu için yeterli fiyat verisi bulunamadı.");
        }

        var model = string.IsNullOrWhiteSpace(_options.Model)
            ? "gemini-3.5-flash"
            : _options.Model.Trim();
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"v1beta/models/{Uri.EscapeDataString(model)}:generateContent");
        request.Headers.TryAddWithoutValidation("x-goog-api-key", _options.ApiKey.Trim());
        request.Content = JsonContent.Create(new
        {
            contents = new[]
            {
                new
                {
                    role = "user",
                    parts = new[] { new { text = BuildPrompt(symbol, candles) } }
                }
            },
            generationConfig = new
            {
                temperature = 0.35,
                maxOutputTokens = 2048,
                responseMimeType = "text/plain",
                thinkingConfig = new
                {
                    thinkingLevel = "low"
                }
            }
        });

        try
        {
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Gemini commentary request for {Symbol} failed with status {StatusCode}.",
                    symbol,
                    (int)response.StatusCode);
                return Failure("AI yorumu şu anda alınamıyor. Lütfen daha sonra yeniden deneyin.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var candidate = ReadFirstCandidate(document.RootElement);
            if (candidate?.FinishReason.Equals("MAX_TOKENS", StringComparison.OrdinalIgnoreCase) == true)
            {
                _logger.LogWarning("Gemini commentary for {Symbol} reached the output token limit.", symbol);
                return Failure(IncompleteResponseMessage);
            }

            var commentary = candidate?.Commentary;
            if (string.IsNullOrWhiteSpace(commentary))
            {
                _logger.LogWarning("Gemini returned no commentary text for {Symbol}.", symbol);
                return Failure("AI servisi bu hisse için yorum üretemedi. Lütfen daha sonra yeniden deneyin.");
            }

            var normalizedCommentary = commentary.Trim();
            if (!HasSummaryHeading(normalizedCommentary))
            {
                _logger.LogWarning("Gemini commentary for {Symbol} did not follow the required Markdown format.", symbol);
                return Failure(IncompleteResponseMessage);
            }

            return new AiCommentaryResult(EnsureDisclaimer(normalizedCommentary), true);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Gemini commentary request timed out for {Symbol}.", symbol);
            return Failure("AI servisi zaman aşımına uğradı. Lütfen daha sonra yeniden deneyin.");
        }
        catch (HttpRequestException exception)
        {
            _logger.LogWarning(exception, "Gemini commentary request failed for {Symbol}.", symbol);
            return Failure("AI servisine şu anda ulaşılamıyor. Lütfen daha sonra yeniden deneyin.");
        }
        catch (JsonException exception)
        {
            _logger.LogWarning(exception, "Gemini returned invalid JSON for {Symbol}.", symbol);
            return Failure("AI servisinden geçerli bir yanıt alınamadı. Lütfen daha sonra yeniden deneyin.");
        }
    }

    private static string BuildPrompt(string symbol, IReadOnlyList<Candle> candles)
    {
        var closes = candles.Select(candle => candle.Close).ToArray();
        var sma20 = IndicatorCalculator.Sma(closes, 20);
        var sma50 = IndicatorCalculator.Sma(closes, 50);
        var sma200 = IndicatorCalculator.Sma(closes, 200);
        var ema12 = IndicatorCalculator.Ema(closes, 12);
        var ema26 = IndicatorCalculator.Ema(closes, 26);
        var rsi14 = IndicatorCalculator.Rsi(closes);
        var macd = IndicatorCalculator.Macd(closes);
        var bollinger = IndicatorCalculator.Bollinger(closes);
        var recentCandles = candles.TakeLast(60).ToArray();
        var builder = new StringBuilder();

        builder.AppendLine("Bir teknik analiz yardımcısısın. Yalnızca aşağıdaki fiyat ve gösterge verilerini kullan.");
        builder.AppendLine($"Hisse: {symbol}");
        builder.AppendLine("Son gösterge değerleri:");
        builder.AppendLine($"- SMA20: {Format(LastValue(sma20))}; SMA50: {Format(LastValue(sma50))}; SMA200: {Format(LastValue(sma200))}");
        builder.AppendLine($"- EMA12: {Format(LastValue(ema12))}; EMA26: {Format(LastValue(ema26))}");
        builder.AppendLine($"- RSI14: {Format(LastValue(rsi14))}");
        builder.AppendLine($"- MACD: {Format(LastValue(macd.Macd))}; Sinyal: {Format(LastValue(macd.Signal))}; Histogram: {Format(LastValue(macd.Histogram))}");
        builder.AppendLine($"- Bollinger üst/orta/alt: {Format(LastValue(bollinger.Upper))} / {Format(LastValue(bollinger.Middle))} / {Format(LastValue(bollinger.Lower))}");
        builder.AppendLine($"- Son kapanış: {Format(recentCandles.LastOrDefault()?.Close)}");
        builder.AppendLine($"- Son 60 günlük en düşük/en yüksek: {Format(recentCandles.Min(candle => candle.Low))} / {Format(recentCandles.Max(candle => candle.High))}");
        builder.AppendLine();
        builder.AppendLine("Son fiyat verileri (tarih, açılış, yüksek, düşük, kapanış, hacim):");

        foreach (var candle in recentCandles)
        {
            builder.AppendLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{candle.Time:yyyy-MM-dd}, {candle.Open:0.####}, {candle.High:0.####}, {candle.Low:0.####}, {candle.Close:0.####}, {candle.Volume}"));
        }

        builder.AppendLine();
        builder.AppendLine("Türkçe, kısa ve aşağıdaki sabit Markdown yapısında bir yorum yaz. Başlıkları ve sıralarını kesinlikle değiştirme, başka başlık ekleme:");
        builder.AppendLine("## Özet");
        builder.AppendLine("Tam olarak 2 cümlelik özet yaz.");
        builder.AppendLine("**Genel Görünüm:** Olumlu/Nötr/Olumsuz seçeneklerinden yalnızca birini yaz.");
        builder.AppendLine("## Trend");
        builder.AppendLine("Fiyatın eğilimini, SMA ve EMA ilişkilerini somut değerlerle kısaca açıkla.");
        builder.AppendLine("## Göstergeler");
        builder.AppendLine("Her göstergeyi kendi maddesinde, güncel okuması ve kısa yorumuyla yaz: RSI 14, MACD/Sinyal/Histogram, SMA20/SMA50/SMA200, EMA12/EMA26 ve Bollinger üst/orta/alt.");
        builder.AppendLine("## Destek ve Direnç");
        builder.AppendLine("Yalnızca verilen OHLC verilerinden türetilmiş somut fiyat seviyeleriyle en az bir destek ve bir direnç yaz.");
        builder.AppendLine("## Riskler");
        builder.AppendLine("Veride görülen başlıca teknik riskleri kısa maddeler halinde yaz.");
        builder.AppendLine("Kesin getiri iddiasında bulunma, al/sat talimatı verme ve haber ya da temel analiz uydurma.");
        builder.AppendLine($"Yanıtı aynen şu cümleyle bitir: {Disclaimer}");
        return builder.ToString();
    }

    private static GeminiCandidateResponse? ReadFirstCandidate(JsonElement root)
    {
        if (!root.TryGetProperty("candidates", out var candidates) ||
            candidates.ValueKind != JsonValueKind.Array ||
            candidates.GetArrayLength() == 0)
        {
            return null;
        }

        var candidate = candidates[0];
        var finishReason = candidate.TryGetProperty("finishReason", out var finishReasonElement)
            ? finishReasonElement.GetString()
            : null;
        string? commentary = null;
        if (candidate.TryGetProperty("content", out var content) &&
            content.TryGetProperty("parts", out var parts) &&
            parts.ValueKind == JsonValueKind.Array)
        {
            var texts = parts.EnumerateArray()
                .Where(part => part.TryGetProperty("text", out _))
                .Select(part => part.GetProperty("text").GetString())
                .Where(text => !string.IsNullOrWhiteSpace(text));
            var combined = string.Join(Environment.NewLine, texts);
            if (!string.IsNullOrWhiteSpace(combined))
            {
                commentary = combined;
            }
        }

        return new GeminiCandidateResponse(commentary, finishReason ?? string.Empty);
    }

    private static bool HasSummaryHeading(string commentary) =>
        commentary.Replace("\r", string.Empty, StringComparison.Ordinal)
            .Split('\n')
            .Any(line => line.Trim().Equals(SummaryHeading, StringComparison.OrdinalIgnoreCase));

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

    private static string Format(decimal? value) =>
        value?.ToString("0.####", CultureInfo.InvariantCulture) ?? "yetersiz veri";

    private static AiCommentaryResult Failure(string message) =>
        new($"{message}\n\n{Disclaimer}", false);

    private static string EnsureDisclaimer(string commentary)
    {
        if (commentary.Contains(Disclaimer, StringComparison.OrdinalIgnoreCase))
        {
            return commentary;
        }

        return $"{commentary.TrimEnd()}\n\n{Disclaimer}";
    }

    private sealed record GeminiCandidateResponse(string? Commentary, string FinishReason);
}
