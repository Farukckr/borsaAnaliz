using BorsaAnaliz.Web.Models;

namespace BorsaAnaliz.Web.Services;

public interface IPortfolioService
{
    Task<IReadOnlyList<Portfolio>> GetPortfoliosAsync(string userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PortfolioSnapshot>> GetSnapshotsAsync(string userId, CancellationToken cancellationToken = default);
    Task<Portfolio> CreatePortfolioAsync(string userId, string name, decimal initialCash = 100_000m, CancellationToken cancellationToken = default);
    Task<PortfolioSnapshot?> GetSnapshotAsync(int portfolioId, string userId, CancellationToken cancellationToken = default);
    Task<TradePreviewResult> GetTradePreviewAsync(int portfolioId, string userId, string symbol, decimal quantity, TransactionType type, CancellationToken cancellationToken = default);
    Task<TradeResult> BuyAsync(int portfolioId, string userId, string symbol, decimal quantity, CancellationToken cancellationToken = default);
    Task<TradeResult> SellAsync(int portfolioId, string userId, string symbol, decimal quantity, CancellationToken cancellationToken = default);
}
