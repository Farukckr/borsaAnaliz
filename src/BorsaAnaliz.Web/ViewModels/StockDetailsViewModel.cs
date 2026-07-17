using BorsaAnaliz.Web.Models;

namespace BorsaAnaliz.Web.ViewModels;

public sealed record StockDetailsViewModel(
    StockSymbol Stock,
    Quote? Quote,
    bool IsWatched,
    IReadOnlyList<KapDisclosure>? KapDisclosures,
    KapCompanyProfile? KapCompanyProfile)
{
    public string CurrencySymbol => Stock.Market.Equals("BIST", StringComparison.OrdinalIgnoreCase) ? "₺" : "$";

    public string ShortSymbol => Stock.Symbol.EndsWith(".IS", StringComparison.OrdinalIgnoreCase)
        ? Stock.Symbol[..^3]
        : Stock.Symbol;
}
