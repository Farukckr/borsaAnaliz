using BorsaAnaliz.Web.Models;

namespace BorsaAnaliz.Web.ViewModels;

public sealed record StockListItemViewModel(StockSymbol Stock, Quote? Quote, bool IsWatched)
{
    public string CurrencySymbol => Stock.Market.Equals("BIST", StringComparison.OrdinalIgnoreCase) ? "₺" : "$";
}
