namespace BorsaAnaliz.Web.ViewModels;

public sealed record StocksIndexViewModel(
    IReadOnlyList<StockListItemViewModel> Stocks,
    string ActiveList,
    int CurrentPage,
    int TotalPages,
    int TotalCount);
