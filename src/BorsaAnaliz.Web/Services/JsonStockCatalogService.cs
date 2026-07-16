using System.Text.Json;
using BorsaAnaliz.Web.Models;

namespace BorsaAnaliz.Web.Services;

public sealed class JsonStockCatalogService : IStockCatalogService
{
    private readonly Lazy<Task<IReadOnlyList<StockSymbol>>> _symbols;

    public JsonStockCatalogService(IWebHostEnvironment environment, ILogger<JsonStockCatalogService> logger)
    {
        var path = Path.Combine(environment.ContentRootPath, "Data", "symbols.json");
        _symbols = new Lazy<Task<IReadOnlyList<StockSymbol>>>(() => LoadAsync(path, logger));
    }

    public async Task<IReadOnlyList<StockSymbol>> GetSymbolsAsync(CancellationToken cancellationToken = default)
    {
        return await _symbols.Value.WaitAsync(cancellationToken);
    }

    private static async Task<IReadOnlyList<StockSymbol>> LoadAsync(
        string path,
        ILogger<JsonStockCatalogService> logger)
    {
        try
        {
            await using var stream = File.OpenRead(path);
            var symbols = await JsonSerializer.DeserializeAsync<List<StockSymbol>>(
                stream,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return symbols?
                .Where(stock => !string.IsNullOrWhiteSpace(stock.Symbol))
                .ToArray() ?? [];
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Hisse sembol kataloğu yüklenemedi: {Path}", path);
            return [];
        }
    }
}
