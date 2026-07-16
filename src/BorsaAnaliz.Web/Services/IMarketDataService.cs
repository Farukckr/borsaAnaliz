using BorsaAnaliz.Web.Models;

namespace BorsaAnaliz.Web.Services;

public interface IMarketDataService
{
    Task<Quote?> GetQuoteAsync(string symbol, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, Quote?>> GetQuotesAsync(
        IEnumerable<string> symbols,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Candle>> GetHistoryAsync(
        string symbol,
        string range,
        string interval,
        CancellationToken cancellationToken = default);
}
