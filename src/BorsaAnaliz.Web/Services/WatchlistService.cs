using BorsaAnaliz.Web.Data;
using BorsaAnaliz.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace BorsaAnaliz.Web.Services;

public sealed class WatchlistService : IWatchlistService
{
    private readonly ApplicationDbContext _db;
    private readonly IStockCatalogService _stockCatalog;

    public WatchlistService(ApplicationDbContext db, IStockCatalogService stockCatalog)
    {
        _db = db;
        _stockCatalog = stockCatalog;
    }

    public async Task<IReadOnlyList<string>> GetSymbolsAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(userId);
        return await _db.WatchlistItems
            .AsNoTracking()
            .Where(item => item.UserId == userId)
            .OrderBy(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .Select(item => item.Symbol)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> ToggleAsync(
        string userId,
        string symbol,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(userId);
        var normalizedSymbol = symbol?.Trim().ToUpperInvariant() ?? string.Empty;
        var catalog = await _stockCatalog.GetSymbolsAsync(cancellationToken);
        var stock = catalog.FirstOrDefault(item =>
            item.Symbol.Equals(normalizedSymbol, StringComparison.OrdinalIgnoreCase));
        if (stock is null)
        {
            throw new ArgumentException("Geçerli bir hisse sembolü seçin.", nameof(symbol));
        }

        var existing = await _db.WatchlistItems.SingleOrDefaultAsync(
            item => item.UserId == userId && item.Symbol == stock.Symbol,
            cancellationToken);
        if (existing is not null)
        {
            _db.WatchlistItems.Remove(existing);
            await _db.SaveChangesAsync(cancellationToken);
            return false;
        }

        _db.WatchlistItems.Add(new WatchlistItem
        {
            UserId = userId,
            Symbol = stock.Symbol,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await _db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> CountAsync(
        string userId,
        CancellationToken cancellationToken = default)
    {
        EnsureUserId(userId);
        return await _db.WatchlistItems.CountAsync(
            item => item.UserId == userId,
            cancellationToken);
    }

    private static void EnsureUserId(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("Kullanıcı bilgisi gereklidir.", nameof(userId));
        }
    }
}
