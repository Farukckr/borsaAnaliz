namespace BorsaAnaliz.Web.Models;

public sealed record CandleResponse(
    long Time,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume);

public sealed record IndicatorPoint(long Time, decimal Value);

public sealed record MacdPoint(long Time, decimal Macd, decimal? Signal, decimal? Histogram);

public sealed record BollingerPoint(long Time, decimal Middle, decimal Upper, decimal Lower);

public sealed record IndicatorSeriesResponse(
    IReadOnlyList<IndicatorPoint> Sma20,
    IReadOnlyList<IndicatorPoint> Sma50,
    IReadOnlyList<IndicatorPoint> Sma200,
    IReadOnlyList<IndicatorPoint> Ema12,
    IReadOnlyList<IndicatorPoint> Ema26,
    IReadOnlyList<IndicatorPoint> Rsi14,
    IReadOnlyList<MacdPoint> Macd,
    IReadOnlyList<BollingerPoint> Bollinger);

public sealed record IndicatorLatestResponse(
    decimal? Sma20,
    decimal? Sma50,
    decimal? Sma200,
    decimal? Ema12,
    decimal? Ema26,
    decimal? Rsi14,
    string RsiSignal,
    decimal? Macd,
    decimal? MacdSignalLine,
    decimal? MacdHistogram,
    string MacdSignal,
    decimal? BollingerUpper,
    decimal? BollingerMiddle,
    decimal? BollingerLower);

public sealed record IndicatorsResponse(
    string Symbol,
    string Range,
    IndicatorSeriesResponse Series,
    IndicatorLatestResponse Latest);
