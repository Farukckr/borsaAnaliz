using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using BorsaAnaliz.Web.Models;
using BorsaAnaliz.Web.Services;
using BorsaAnaliz.Web.ViewModels;

namespace BorsaAnaliz.Web.Controllers;

public class HomeController : Controller
{
    private static readonly MarketSnapshotDefinition[] MarketSnapshots =
    [
        new("XU100.IS", "BIST 100", "Türkiye hisse piyasası", "bi-bar-chart-fill", "", " puan", 2),
        new("USDTRY=X", "USD / TRY", "Dolar kuru", "bi-currency-exchange", "₺", "", 4),
        new("^GSPC", "S&P 500", "ABD geniş piyasa", "bi-globe-americas", "", " puan", 2),
        new("^NDX", "NASDAQ 100", "ABD teknoloji", "bi-cpu-fill", "", " puan", 2)
    ];

    private readonly ILogger<HomeController> _logger;
    private readonly IStockCatalogService _stockCatalog;
    private readonly IMarketDataService _marketData;

    public HomeController(
        ILogger<HomeController> logger,
        IStockCatalogService stockCatalog,
        IMarketDataService marketData)
    {
        _logger = logger;
        _stockCatalog = stockCatalog;
        _marketData = marketData;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var xu100 = await _stockCatalog.GetByIndexAsync("XU100", cancellationToken);
        var us = await _stockCatalog.GetByMarketAsync("US", cancellationToken);
        var symbols = xu100
            .Concat(us)
            .DistinctBy(stock => stock.Symbol, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var requestedSymbols = symbols
            .Select(stock => stock.Symbol)
            .Concat(MarketSnapshots.Select(snapshot => snapshot.Symbol));
        var quotes = await _marketData.GetQuotesAsync(requestedSymbols, cancellationToken);

        var stocks = symbols
            .Select(stock => new StockListItemViewModel(
                stock,
                quotes.GetValueOrDefault(stock.Symbol)))
            .Where(item => item.Quote?.ChangePercent is not null)
            .ToArray();
        var gainers = stocks
            .OrderByDescending(item => item.Quote!.ChangePercent)
            .Take(5)
            .ToArray();
        var losers = stocks
            .OrderBy(item => item.Quote!.ChangePercent)
            .Take(5)
            .ToArray();
        var snapshots = MarketSnapshots
            .Select(snapshot => new MarketSnapshotViewModel(
                snapshot.Symbol,
                snapshot.DisplayName,
                snapshot.Description,
                snapshot.IconClass,
                snapshot.PricePrefix,
                snapshot.PriceSuffix,
                snapshot.PriceDecimals,
                quotes.GetValueOrDefault(snapshot.Symbol)))
            .ToArray();
        var marketTimes = quotes.Values
            .Where(quote => quote?.MarketTime is not null)
            .Select(quote => quote!.MarketTime!.Value)
            .ToArray();

        return View(new HomeDashboardViewModel(
            snapshots,
            gainers,
            losers,
            marketTimes.Length > 0 ? marketTimes.Max() : null));
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private sealed record MarketSnapshotDefinition(
        string Symbol,
        string DisplayName,
        string Description,
        string IconClass,
        string PricePrefix,
        string PriceSuffix,
        int PriceDecimals);
}
