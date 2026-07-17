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
        var activeTab = tab?.Trim().ToLowerInvariant() switch
        {
            "geri-alimlar" => "geri-alimlar",
            "temettu" => "temettu",
            "sermaye-artirimlari" => "sermaye-artirimlari",
            _ => "tum"
        };
        var disclosures = activeTab switch
        {
            "geri-alimlar" => await _kapNews.GetBuybacksAsync(cancellationToken: cancellationToken),
            "temettu" => await _kapNews.GetDividendsAsync(cancellationToken: cancellationToken),
            "sermaye-artirimlari" => await _kapNews.GetCapitalIncreasesAsync(cancellationToken: cancellationToken),
            _ => await _kapNews.GetLatestAsync(cancellationToken)
        };
        return View(new NewsIndexViewModel(activeTab, disclosures));
    }
}
