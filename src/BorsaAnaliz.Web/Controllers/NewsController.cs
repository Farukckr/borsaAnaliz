using BorsaAnaliz.Web.Services;
using BorsaAnaliz.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace BorsaAnaliz.Web.Controllers;

public sealed class NewsController : Controller
{
    private readonly IKapNewsService _kapNews;

    public NewsController(IKapNewsService kapNews)
    {
        _kapNews = kapNews;
    }

    [HttpGet("/Haberler")]
    public async Task<IActionResult> Index(string? tab, CancellationToken cancellationToken)
    {
        var activeTab = tab?.Equals("geri-alimlar", StringComparison.OrdinalIgnoreCase) == true
            ? "geri-alimlar"
            : "tum";
        var disclosures = activeTab == "geri-alimlar"
            ? await _kapNews.GetBuybacksAsync(cancellationToken: cancellationToken)
            : await _kapNews.GetLatestAsync(cancellationToken);
        return View(new NewsIndexViewModel(activeTab, disclosures));
    }
}
