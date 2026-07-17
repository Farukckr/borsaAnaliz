using BorsaAnaliz.Web.Models;

namespace BorsaAnaliz.Web.Services;

public interface IAiCommentaryService
{
    bool IsConfigured { get; }

    Task<AiCommentaryResult> GetCommentaryAsync(
        string symbol,
        IReadOnlyList<Candle> candles,
        IReadOnlyList<string>? recentDisclosureLines = null,
        CancellationToken cancellationToken = default);
}

public sealed record AiCommentaryResult(string Commentary, bool Succeeded);
