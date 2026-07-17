namespace BorsaAnaliz.Web.Models;

public sealed record AiCommentaryResponse(
    string Commentary,
    bool Cached,
    DateTimeOffset GeneratedAt,
    bool Succeeded,
    bool IncludesRecentDisclosures = false);
