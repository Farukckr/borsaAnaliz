using BorsaAnaliz.Web.Models;

namespace BorsaAnaliz.Web.Services;

public interface IAiCommentaryService
{
    bool IsConfigured { get; }

    Task<AiCommentaryResult> GetCommentaryAsync(
        string symbol,
        IReadOnlyList<Candle> candles,
        CancellationToken cancellationToken = default);
}

public sealed record AiCommentaryResult(string Commentary, bool Succeeded);
