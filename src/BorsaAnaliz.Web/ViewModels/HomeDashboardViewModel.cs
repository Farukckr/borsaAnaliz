using BorsaAnaliz.Web.Models;

namespace BorsaAnaliz.Web.ViewModels;

public sealed record HomeDashboardViewModel(
    IReadOnlyList<MarketSnapshotViewModel> MarketSnapshots,
    IReadOnlyList<StockListItemViewModel> Gainers,
    IReadOnlyList<StockListItemViewModel> Losers,
    IReadOnlyList<KapDisclosure> LatestDisclosures,
    DateTimeOffset? LastUpdatedAt);

public sealed record MarketSnapshotViewModel(
    string Symbol,
    string DisplayName,
    string Description,
    string IconClass,
    string PricePrefix,
    string PriceSuffix,
    int PriceDecimals,
    Quote? Quote);
