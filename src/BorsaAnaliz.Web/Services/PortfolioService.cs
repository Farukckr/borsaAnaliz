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

    public async Task<IReadOnlyList<PortfolioSnapshot>> GetSnapshotsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        var portfolios = await GetPortfoliosAsync(userId, cancellationToken);
        var snapshots = new List<PortfolioSnapshot>(portfolios.Count);
        foreach (var portfolio in portfolios)
        {
            var snapshot = await GetSnapshotAsync(portfolio.Id, userId, cancellationToken);
            if (snapshot is not null)
            {
                snapshots.Add(snapshot);
            }
        }

        return snapshots;
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
        var ledger = CalculateLedger(portfolio.InitialCash, orderedTransactions);
        var openSymbols = ledger.Positions
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
            var ledgerPosition = ledger.Positions[symbol];
            var averageCost = ledgerPosition.TotalCost / ledgerPosition.Quantity;
            var quote = quotes.GetValueOrDefault(symbol);
            var currentPrice = quote?.Price;
            hasMissingQuotes |= currentPrice is null;
            var effectivePrice = currentPrice ?? averageCost;
            var value = effectivePrice * ledgerPosition.Quantity;
            var profitLoss = currentPrice is null
                ? (decimal?)null
                : (currentPrice.Value - averageCost) * ledgerPosition.Quantity;
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
                ledgerPosition.Quantity,
                averageCost,
                currentPrice,
                value,
                profitLoss,
                profitLossPercent,
                quote?.Change is decimal dailyChange ? dailyChange * ledgerPosition.Quantity : 0m,
                quote?.ChangePercent,
                0m,
                ledgerPosition.TotalCost,
                ledgerPosition.RealizedProfitLoss,
                ledgerPosition.FirstPurchaseDate));
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
        var totalValue = ledger.Cash + positions.Sum(position => position.Value);
        positions = positions
            .Select(position => position with
            {
                WeightPercent = totalValue == 0 ? 0m : position.Value / totalValue * 100m
            })
            .ToList();
        var dayChange = positions.Sum(position => position.DailyChange);
        var previousTotalValue = totalValue - dayChange;
        var dayChangePercent = previousTotalValue == 0 ? 0m : dayChange / previousTotalValue * 100m;
        var totalUnrealizedProfitLoss = positions.Sum(position => position.UnrealizedProfitLoss ?? 0m);
        var totalCostBasis = positions.Sum(position => position.TotalCostBasis);
        var totalProfitLoss = totalValue - portfolio.InitialCash;
        var totalReturnPercent = portfolio.InitialCash == 0 ? 0m : totalProfitLoss / portfolio.InitialCash * 100m;

        return new PortfolioSnapshot(
            portfolio,
            ledger.Cash,
            positions,
            transactionItems,
            totalValue,
            totalProfitLoss,
            totalReturnPercent,
            hasMissingQuotes,
            dayChange,
            dayChangePercent,
            totalUnrealizedProfitLoss,
            ledger.TotalRealizedProfitLoss,
            totalCostBasis,
            positions.Count);
    }

    public async Task<TradePreviewResult> GetTradePreviewAsync(
        int portfolioId,
        string userId,
        string symbol,
        decimal quantity,
        TransactionType type,
        CancellationToken cancellationToken = default)
    {
        var portfolio = await _db.Portfolios.AsNoTracking()
            .Include(item => item.Transactions)
            .SingleOrDefaultAsync(item => item.Id == portfolioId && item.UserId == userId, cancellationToken);
        if (portfolio is null)
        {
            return TradePreviewResult.NotFound();
        }

        if (quantity <= 0)
        {
            return TradePreviewResult.Invalid("Miktar sıfırdan büyük olmalıdır.");
        }

        var normalizedSymbol = symbol.Trim().ToUpperInvariant();
        var catalog = await _stockCatalog.GetSymbolsAsync(cancellationToken);
        var stock = catalog.FirstOrDefault(item =>
            item.Symbol.Equals(normalizedSymbol, StringComparison.OrdinalIgnoreCase));
        if (stock is null)
        {
            return TradePreviewResult.Invalid("Geçerli bir hisse sembolü seçin.");
        }

        var quote = await _marketData.GetQuoteAsync(normalizedSymbol, cancellationToken);
        if (quote is null)
        {
            return TradePreviewResult.Invalid("Güncel piyasa fiyatı alınamadı.");
        }

        var ledger = CalculateLedger(portfolio.InitialCash, portfolio.Transactions);
        var ownedQuantity = ledger.Positions.GetValueOrDefault(normalizedSymbol)?.Quantity ?? 0m;
        var estimatedTotal = quote.Price * quantity;
        var cashAfterTrade = type == TransactionType.Buy
            ? ledger.Cash - estimatedTotal
            : ledger.Cash + estimatedTotal;
        var canExecute = type == TransactionType.Buy
            ? estimatedTotal <= ledger.Cash
            : quantity <= ownedQuantity;
        var warningMessage = canExecute
            ? null
            : type == TransactionType.Buy
                ? "Bu alım için kullanılabilir nakit yetersiz."
                : $"Satılabilir miktar {ownedQuantity:N4}.";

        return TradePreviewResult.Success(new TradePreview(
            normalizedSymbol,
            GetCurrencySymbol(stock.Market),
            quote.Price,
            estimatedTotal,
            ledger.Cash,
            cashAfterTrade,
            ownedQuantity,
            canExecute,
            warningMessage));
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
            var cash = CalculateLedger(portfolio.InitialCash, portfolio.Transactions).Cash;
            var requiredCash = quote.Price * quantity;
            if (requiredCash > cash)
            {
                return TradeResult.Failure($"Yetersiz nakit. İşlem için {requiredCash:N2}, kullanılabilir {cash:N2}.");
            }
        }
        else
        {
            var ledger = CalculateLedger(portfolio.InitialCash, portfolio.Transactions);
            var ownedQuantity = ledger.Positions.GetValueOrDefault(normalizedSymbol)?.Quantity ?? 0m;
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

    public static PortfolioLedger CalculateLedger(decimal initialCash, IEnumerable<Transaction> transactions)
    {
        var positions = new Dictionary<string, PositionAccumulator>(StringComparer.OrdinalIgnoreCase);
        var cash = initialCash;
        foreach (var transaction in transactions
                     .OrderBy(item => item.ExecutedAt)
                     .ThenBy(item => item.Id))
        {
            if (!positions.TryGetValue(transaction.Symbol, out var position))
            {
                position = new PositionAccumulator();
                positions[transaction.Symbol] = position;
            }

            if (transaction.Type == TransactionType.Buy)
            {
                cash -= transaction.Quantity * transaction.Price;
                position.FirstPurchaseDate ??= transaction.ExecutedAt;
                position.TotalCost += transaction.Quantity * transaction.Price;
                position.Quantity += transaction.Quantity;
                continue;
            }

            cash += transaction.Quantity * transaction.Price;

            if (position.Quantity <= 0)
            {
                continue;
            }

            var averageCost = position.TotalCost / position.Quantity;
            var soldQuantity = Math.Min(transaction.Quantity, position.Quantity);
            position.RealizedProfitLoss += (transaction.Price - averageCost) * soldQuantity;
            position.Quantity -= soldQuantity;
            position.TotalCost -= averageCost * soldQuantity;
            if (position.Quantity == 0)
            {
                position.TotalCost = 0;
            }
        }

        var ledgerPositions = positions.ToDictionary(
            entry => entry.Key,
            entry => new PortfolioLedgerPosition(
                entry.Value.Quantity,
                entry.Value.TotalCost,
                entry.Value.RealizedProfitLoss,
                entry.Value.FirstPurchaseDate),
            StringComparer.OrdinalIgnoreCase);

        return new PortfolioLedger(
            cash,
            ledgerPositions,
            ledgerPositions.Values.Sum(position => position.RealizedProfitLoss));
    }

    private static string GetCurrencySymbol(string market) =>
        market.Equals("BIST", StringComparison.OrdinalIgnoreCase) ? "₺" : "$";

    private sealed class PositionAccumulator
    {
        public decimal Quantity { get; set; }
        public decimal TotalCost { get; set; }
        public decimal RealizedProfitLoss { get; set; }
        public DateTimeOffset? FirstPurchaseDate { get; set; }
    }
}
