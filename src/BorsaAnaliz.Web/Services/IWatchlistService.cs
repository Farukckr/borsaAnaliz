namespace BorsaAnaliz.Web.Services;

public interface IWatchlistService
{
    Task<IReadOnlyList<string>> GetSymbolsAsync(
        string userId,
        CancellationToken cancellationToken = default);

    Task<bool> ToggleAsync(
        string userId,
        string symbol,
        CancellationToken cancellationToken = default);

    Task<int> CountAsync(
        string userId,
        CancellationToken cancellationToken = default);
}
