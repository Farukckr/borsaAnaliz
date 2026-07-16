namespace BorsaAnaliz.Web.Models;

public sealed record PortfolioPosition(
    string Symbol,
    string Name,
    string Market,
    string CurrencySymbol,
    decimal Quantity,
    decimal AverageCost,
    decimal? CurrentPrice,
    decimal Value,
    decimal? UnrealizedProfitLoss,
    decimal? UnrealizedProfitLossPercent);

public sealed record PortfolioTransactionItem(
    int Id,
    string Symbol,
    string Name,
    string CurrencySymbol,
    TransactionType Type,
    decimal Quantity,
    decimal Price,
    DateTimeOffset ExecutedAt);

public sealed record PortfolioSnapshot(
    Portfolio Portfolio,
    decimal Cash,
    IReadOnlyList<PortfolioPosition> Positions,
    IReadOnlyList<PortfolioTransactionItem> Transactions,
    decimal TotalValue,
    decimal TotalProfitLoss,
    decimal TotalReturnPercent,
    bool HasMissingQuotes);

public sealed record TradeResult(bool Succeeded, string? ErrorMessage)
{
    public static TradeResult Success() => new(true, null);
    public static TradeResult Failure(string message) => new(false, message);
}
