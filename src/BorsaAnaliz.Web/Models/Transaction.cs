namespace BorsaAnaliz.Web.Models;

public enum TransactionType
{
    Buy = 1,
    Sell = 2
}

public sealed class Transaction
{
    public int Id { get; set; }
    public int PortfolioId { get; set; }
    public Portfolio Portfolio { get; set; } = null!;
    public string Symbol { get; set; } = string.Empty;
    public TransactionType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal Price { get; set; }
    public DateTimeOffset ExecutedAt { get; set; } = DateTimeOffset.UtcNow;
}
