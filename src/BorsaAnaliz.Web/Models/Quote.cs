namespace BorsaAnaliz.Web.Models;

public sealed record Quote(
    string Symbol,
    decimal Price,
    decimal? PreviousClose,
    decimal? Change,
    decimal? ChangePercent,
    string Currency,
    DateTimeOffset? MarketTime);
