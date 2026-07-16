namespace BorsaAnaliz.Web.Models;

public sealed record Candle(
    DateTimeOffset Time,
    decimal Open,
    decimal High,
    decimal Low,
    decimal Close,
    long Volume);
