namespace BorsaAnaliz.Web.Models;

public sealed class WatchlistItem
{
    public int Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string Symbol { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
