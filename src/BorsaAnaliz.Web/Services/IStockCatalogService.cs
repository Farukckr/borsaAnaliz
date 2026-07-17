using BorsaAnaliz.Web.Models;

namespace BorsaAnaliz.Web.Services;

public interface IStockCatalogService
{
    Task<IReadOnlyList<StockSymbol>> GetSymbolsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockSymbol>> GetByIndexAsync(
        string indexCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StockSymbol>> GetByMarketAsync(
        string market,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetSectorsAsync(
        CancellationToken cancellationToken = default);
}
