using System.Security.Claims;
using BorsaAnaliz.Web.Services;
using BorsaAnaliz.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BorsaAnaliz.Web.Controllers;

[Authorize]
public sealed class PortfolioController : Controller
{
    private readonly IPortfolioService _portfolioService;
    private readonly IStockCatalogService _stockCatalog;

    public PortfolioController(IPortfolioService portfolioService, IStockCatalogService stockCatalog)
    {
        _portfolioService = portfolioService;
        _stockCatalog = stockCatalog;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var snapshots = await _portfolioService.GetSnapshotsAsync(GetUserId(), cancellationToken);
        return View(snapshots);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new CreatePortfolioViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreatePortfolioViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var portfolio = await _portfolioService.CreatePortfolioAsync(
            GetUserId(),
            model.Name,
            model.InitialCash,
            cancellationToken);
        TempData["SuccessMessage"] = "Portföy oluşturuldu.";
        return RedirectToAction(nameof(Details), new { id = portfolio.Id });
    }

    public async Task<IActionResult> Details(int id, CancellationToken cancellationToken)
    {
        var snapshot = await _portfolioService.GetSnapshotAsync(id, GetUserId(), cancellationToken);
        return snapshot is null ? NotFound() : View(snapshot);
    }

    [HttpGet]
    public async Task<IActionResult> Trade(
        int? portfolioId,
        string? symbol,
        CancellationToken cancellationToken)
    {
        var model = new TradeViewModel
        {
            PortfolioId = portfolioId ?? 0,
            Symbol = symbol?.Trim().ToUpperInvariant() ?? string.Empty
        };
        await PopulateTradeOptionsAsync(model, cancellationToken);
        return View(model);
    }

    [HttpGet("/api/portfolios/{id:int}/value-history")]
    public async Task<IActionResult> ValueHistory(
        int id,
        CancellationToken cancellationToken = default)
    {
        var points = await _portfolioService.GetValueHistoryAsync(
            id,
            GetUserId(),
            cancellationToken);
        return points is null ? NotFound() : Ok(points);
    }

    [HttpGet("/api/portfolios/{portfolioId:int}/trade-preview")]
    public async Task<IActionResult> TradePreview(
        int portfolioId,
        [FromQuery] string symbol,
        [FromQuery] decimal quantity,
        [FromQuery] string type = "buy",
        CancellationToken cancellationToken = default)
    {
        var transactionType = type.Equals("sell", StringComparison.OrdinalIgnoreCase)
            ? Models.TransactionType.Sell
            : Models.TransactionType.Buy;
        var result = await _portfolioService.GetTradePreviewAsync(
            portfolioId,
            GetUserId(),
            symbol ?? string.Empty,
            quantity,
            transactionType,
            cancellationToken);

        if (!result.PortfolioFound)
        {
            return NotFound();
        }

        return result.Preview is null
            ? BadRequest(new { message = result.ErrorMessage ?? "İşlem önizlemesi hazırlanamadı." })
            : Ok(result.Preview);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Buy(TradeViewModel model, CancellationToken cancellationToken)
    {
        return ExecuteTradeAsync(model, isBuy: true, cancellationToken);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> Sell(TradeViewModel model, CancellationToken cancellationToken)
    {
        return ExecuteTradeAsync(model, isBuy: false, cancellationToken);
    }

    private async Task<IActionResult> ExecuteTradeAsync(
        TradeViewModel model,
        bool isBuy,
        CancellationToken cancellationToken)
    {
        model.Symbol = model.Symbol?.Trim().ToUpperInvariant() ?? string.Empty;
        if (ModelState.IsValid)
        {
            var result = isBuy
                ? await _portfolioService.BuyAsync(
                    model.PortfolioId,
                    GetUserId(),
                    model.Symbol,
                    model.Quantity,
                    cancellationToken)
                : await _portfolioService.SellAsync(
                    model.PortfolioId,
                    GetUserId(),
                    model.Symbol,
                    model.Quantity,
                    cancellationToken);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = isBuy ? "Alım işlemi tamamlandı." : "Satış işlemi tamamlandı.";
                return RedirectToAction(nameof(Details), new { id = model.PortfolioId });
            }

            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "İşlem tamamlanamadı.");
        }

        await PopulateTradeOptionsAsync(model, cancellationToken);
        return View("Trade", model);
    }

    private async Task PopulateTradeOptionsAsync(TradeViewModel model, CancellationToken cancellationToken)
    {
        var portfolios = await _portfolioService.GetPortfoliosAsync(GetUserId(), cancellationToken);
        if (model.PortfolioId == 0 && portfolios.Count > 0)
        {
            model.PortfolioId = portfolios[0].Id;
        }

        model.PortfolioOptions = portfolios
            .Select(portfolio => new SelectListItem(portfolio.Name, portfolio.Id.ToString(), portfolio.Id == model.PortfolioId))
            .ToArray();
        model.Symbols = await _stockCatalog.GetSymbolsAsync(cancellationToken);
    }

    private string GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new InvalidOperationException("Oturum açmış kullanıcı kimliği bulunamadı.");
    }
}
