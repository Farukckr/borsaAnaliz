namespace BorsaAnaliz.Web.Models;

public sealed class StockSymbol
{
    public string Symbol { get; init; } = string.Empty;

    public string TvSymbol { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string Market { get; init; } = string.Empty;
}
