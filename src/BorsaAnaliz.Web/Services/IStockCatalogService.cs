using BorsaAnaliz.Web.Models;

namespace BorsaAnaliz.Web.Services;

public interface IStockCatalogService
{
    Task<IReadOnlyList<StockSymbol>> GetSymbolsAsync(CancellationToken cancellationToken = default);
}
