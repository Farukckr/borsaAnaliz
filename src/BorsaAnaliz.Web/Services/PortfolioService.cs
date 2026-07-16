using BorsaAnaliz.Web.Data;
using BorsaAnaliz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BorsaAnaliz.Web.Services;

public sealed class PortfolioService : IPortfolioService
{
    private readonly ApplicationDbContext _db;
    private readonly IMarketDataService _marketData;
    private readonly IStockCatalogService _stockCatalog;

    public PortfolioService(ApplicationDbContext db, IMarketDataService marketData, IStockCatalogService stockCatalog)
    {
        _db = db;
        _marketData = marketData;
        _stockCatalog = stockCatalog;
    }

    public async Task<IReadOnlyList<Portfolio>> GetPortfoliosAsync(string userId, CancellationToken cancellationToken = default)
    {
        var portfolios = await _db.Portfolios.AsNoTracking()
            .Where(portfolio => portfolio.UserId == userId)
            .OrderBy(portfolio => portfolio.CreatedAt)
            .ToListAsync(cancellationToken);

        return portfolios;
    }

    public async Task<Portfolio> CreatePortfolioAsync(
        string userId,
        string name,
        decimal initialCash = 100_000m,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("Kullanıcı bilgisi gereklidir.", nameof(userId));
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Portföy adı gereklidir.", nameof(name));
        }

        if (initialCash <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialCash));
        }

        var portfolio = new Portfolio
        {
            UserId = userId,
            Name = name.Trim(),
            InitialCash = initialCash,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _db.Portfolios.Add(portfolio);
        await _db.SaveChangesAsync(cancellationToken);
        return portfolio;
    }

    public async Task<PortfolioSnapshot?> GetSnapshotAsync(
        int portfolioId,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var portfolio = await _db.Portfolios.AsNoTracking()
            .Include(item => item.Transactions)
            .SingleOrDefaultAsync(item => item.Id == portfolioId && item.UserId == userId, cancellationToken);
        if (portfolio is null)
        {
            return null;
        }

        var orderedTransactions = portfolio.Transactions
            .OrderBy(transaction => transaction.ExecutedAt)
            .ThenBy(transaction => transaction.Id)
            .ToArray();
        var cash = CalculateCash(portfolio.InitialCash, orderedTransactions);
        var accumulators = BuildPositions(orderedTransactions);
        var openSymbols = accumulators
            .Where(entry => entry.Value.Quantity > 0)
            .Select(entry => entry.Key)
            .ToArray();
        var quotes = await _marketData.GetQuotesAsync(openSymbols, cancellationToken);
        var catalog = await _stockCatalog.GetSymbolsAsync(cancellationToken);
        var catalogBySymbol = catalog.ToDictionary(stock => stock.Symbol, StringComparer.OrdinalIgnoreCase);
        var positions = new List<PortfolioPosition>();
        var hasMissingQuotes = false;

        foreach (var symbol in openSymbols.Order(StringComparer.OrdinalIgnoreCase))
        {
            var accumulator = accumulators[symbol];
            var averageCost = accumulator.TotalCost / accumulator.Quantity;
            var quote = quotes.GetValueOrDefault(symbol);
            var currentPrice = quote?.Price;
            hasMissingQuotes |= currentPrice is null;
            var effectivePrice = currentPrice ?? averageCost;
            var value = effectivePrice * accumulator.Quantity;
            var profitLoss = currentPrice is null
                ? (decimal?)null
                : (currentPrice.Value - averageCost) * accumulator.Quantity;
            var profitLossPercent = currentPrice is null || averageCost == 0
                ? (decimal?)null
                : ((currentPrice.Value - averageCost) / averageCost) * 100m;
            catalogBySymbol.TryGetValue(symbol, out var stock);
            var market = stock?.Market ?? (symbol.EndsWith(".IS", StringComparison.OrdinalIgnoreCase) ? "BIST" : "US");

            positions.Add(new PortfolioPosition(
                symbol,
                stock?.Name ?? symbol,
                market,
                GetCurrencySymbol(market),
                accumulator.Quantity,
                averageCost,
                currentPrice,
                value,
                profitLoss,
                profitLossPercent));
        }

        var transactionItems = orderedTransactions
            .OrderByDescending(transaction => transaction.ExecutedAt)
            .ThenByDescending(transaction => transaction.Id)
            .Select(transaction =>
            {
                catalogBySymbol.TryGetValue(transaction.Symbol, out var stock);
                var market = stock?.Market ?? (transaction.Symbol.EndsWith(".IS", StringComparison.OrdinalIgnoreCase) ? "BIST" : "US");
                return new PortfolioTransactionItem(
                    transaction.Id,
                    transaction.Symbol,
                    stock?.Name ?? transaction.Symbol,
                    GetCurrencySymbol(market),
                    transaction.Type,
                    transaction.Quantity,
                    transaction.Price,
                    transaction.ExecutedAt);
            })
            .ToArray();
        var totalValue = cash + positions.Sum(position => position.Value);
        var totalProfitLoss = totalValue - portfolio.InitialCash;
        var totalReturnPercent = portfolio.InitialCash == 0 ? 0m : totalProfitLoss / portfolio.InitialCash * 100m;

        return new PortfolioSnapshot(
            portfolio,
            cash,
            positions,
            transactionItems,
            totalValue,
            totalProfitLoss,
            totalReturnPercent,
            hasMissingQuotes);
    }

    public Task<TradeResult> BuyAsync(
        int portfolioId,
        string userId,
        string symbol,
        decimal quantity,
        CancellationToken cancellationToken = default)
    {
        return ExecuteTradeAsync(portfolioId, userId, symbol, quantity, TransactionType.Buy, cancellationToken);
    }

    public Task<TradeResult> SellAsync(
        int portfolioId,
        string userId,
        string symbol,
        decimal quantity,
        CancellationToken cancellationToken = default)
    {
        return ExecuteTradeAsync(portfolioId, userId, symbol, quantity, TransactionType.Sell, cancellationToken);
    }

    private async Task<TradeResult> ExecuteTradeAsync(
        int portfolioId,
        string userId,
        string symbol,
        decimal quantity,
        TransactionType type,
        CancellationToken cancellationToken)
    {
        if (quantity <= 0)
        {
            return TradeResult.Failure("Miktar sıfırdan büyük olmalıdır.");
        }

        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        var catalog = await _stockCatalog.GetSymbolsAsync(cancellationToken);
        if (!catalog.Any(stock => stock.Symbol.Equals(normalizedSymbol, StringComparison.OrdinalIgnoreCase)))
        {
            return TradeResult.Failure("Geçerli bir hisse sembolü seçin.");
        }

        var quote = await _marketData.GetQuoteAsync(normalizedSymbol, cancellationToken);
        if (quote is null)
        {
            return TradeResult.Failure("Güncel piyasa fiyatı alınamadı. Lütfen daha sonra tekrar deneyin.");
        }

        await using var databaseTransaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        var portfolio = await _db.Portfolios.Include(item => item.Transactions)
            .SingleOrDefaultAsync(item => item.Id == portfolioId && item.UserId == userId, cancellationToken);
        if (portfolio is null)
        {
            return TradeResult.Failure("Portföy bulunamadı.");
        }

        if (type == TransactionType.Buy)
        {
            var cash = CalculateCash(portfolio.InitialCash, portfolio.Transactions);
            var requiredCash = quote.Price * quantity;
            if (requiredCash > cash)
            {
                return TradeResult.Failure($"Yetersiz nakit. İşlem için {requiredCash:N2}, kullanılabilir {cash:N2}.");
            }
        }
        else
        {
            var ownedQuantity = portfolio.Transactions
                .Where(transaction => transaction.Symbol.Equals(normalizedSymbol, StringComparison.OrdinalIgnoreCase))
                .Sum(transaction => transaction.Type == TransactionType.Buy ? transaction.Quantity : -transaction.Quantity);
            if (quantity > ownedQuantity)
            {
                return TradeResult.Failure($"Yetersiz hisse. Satılabilir miktar {ownedQuantity:N4}.");
            }
        }

        portfolio.Transactions.Add(new Transaction
        {
            Symbol = normalizedSymbol,
            Type = type,
            Quantity = quantity,
            Price = quote.Price,
            ExecutedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        await databaseTransaction.CommitAsync(cancellationToken);
        return TradeResult.Success();
    }

    private static decimal CalculateCash(decimal initialCash, IEnumerable<Transaction> transactions)
    {
        return transactions.Aggregate(initialCash, (cash, transaction) =>
            transaction.Type == TransactionType.Buy
                ? cash - (transaction.Quantity * transaction.Price)
                : cash + (transaction.Quantity * transaction.Price));
    }

    private static Dictionary<string, PositionAccumulator> BuildPositions(IEnumerable<Transaction> transactions)
    {
        var positions = new Dictionary<string, PositionAccumulator>(StringComparer.OrdinalIgnoreCase);
        foreach (var transaction in transactions)
        {
            if (!positions.TryGetValue(transaction.Symbol, out var position))
            {
                position = new PositionAccumulator();
                positions[transaction.Symbol] = position;
            }

            if (transaction.Type == TransactionType.Buy)
            {
                position.TotalCost += transaction.Quantity * transaction.Price;
                position.Quantity += transaction.Quantity;
                continue;
            }

            if (position.Quantity <= 0)
            {
                continue;
            }

            var averageCost = position.TotalCost / position.Quantity;
            var soldQuantity = Math.Min(transaction.Quantity, position.Quantity);
            position.Quantity -= soldQuantity;
            position.TotalCost -= averageCost * soldQuantity;
            if (position.Quantity == 0)
            {
                position.TotalCost = 0;
            }
        }

        return positions;
    }

    private static string GetCurrencySymbol(string market) =>
        market.Equals("BIST", StringComparison.OrdinalIgnoreCase) ? "₺" : "$";

    private sealed class PositionAccumulator
    {
        public decimal Quantity { get; set; }
        public decimal TotalCost { get; set; }
    }
}
