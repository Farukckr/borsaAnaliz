namespace BorsaAnaliz.Web.Models;

public sealed class Portfolio
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal InitialCash { get; set; } = 100_000m;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}
