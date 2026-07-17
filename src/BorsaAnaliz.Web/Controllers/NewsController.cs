using BorsaAnaliz.Web.Services;
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
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var disclosures = await _kapNews.GetLatestAsync(cancellationToken);
        return View(disclosures);
    }
}
