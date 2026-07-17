namespace BorsaAnaliz.Web.ViewModels;

public sealed record StocksIndexViewModel(
    IReadOnlyList<StockListItemViewModel> Stocks,
    string ActiveList,
    IReadOnlyList<string> Sectors,
    string? ActiveSector,
    int CurrentPage,
    int TotalPages,
    int TotalCount);
