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
    decimal? UnrealizedProfitLossPercent,
    decimal DailyChange,
    decimal? DailyChangePercent,
    decimal WeightPercent,
    decimal TotalCostBasis,
    decimal RealizedProfitLoss,
    DateTimeOffset? FirstPurchaseDate);

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
    bool HasMissingQuotes,
    decimal DayChange,
    decimal DayChangePercent,
    decimal TotalUnrealizedProfitLoss,
    decimal TotalRealizedProfitLoss,
    decimal TotalCostBasis,
    int PositionCount);

public sealed record PortfolioLedgerPosition(
    decimal Quantity,
    decimal TotalCost,
    decimal RealizedProfitLoss,
    DateTimeOffset? FirstPurchaseDate);

public sealed record PortfolioLedger(
    decimal Cash,
    IReadOnlyDictionary<string, PortfolioLedgerPosition> Positions,
    decimal TotalRealizedProfitLoss);

public sealed record TradePreview(
    string Symbol,
    string CurrencySymbol,
    decimal CurrentPrice,
    decimal EstimatedTotal,
    decimal Cash,
    decimal CashAfterTrade,
    decimal OwnedQuantity,
    bool CanExecute,
    string? WarningMessage);

public sealed record TradePreviewResult(bool PortfolioFound, TradePreview? Preview, string? ErrorMessage)
{
    public static TradePreviewResult Success(TradePreview preview) => new(true, preview, null);
    public static TradePreviewResult Invalid(string message) => new(true, null, message);
    public static TradePreviewResult NotFound() => new(false, null, null);
}

public sealed record TradeResult(bool Succeeded, string? ErrorMessage)
{
    public static TradeResult Success() => new(true, null);
    public static TradeResult Failure(string message) => new(false, message);
}
