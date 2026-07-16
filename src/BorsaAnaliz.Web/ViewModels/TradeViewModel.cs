using System.ComponentModel.DataAnnotations;
using BorsaAnaliz.Web.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace BorsaAnaliz.Web.ViewModels;

public sealed class TradeViewModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Bir portföy seçin.")]
    [Display(Name = "Portföy")]
    public int PortfolioId { get; set; }

    [Required(ErrorMessage = "Hisse sembolü gereklidir.")]
    [StringLength(32)]
    [Display(Name = "Hisse")]
    public string Symbol { get; set; } = string.Empty;

    [Range(0.0001, 100000000, ErrorMessage = "Miktar sıfırdan büyük olmalıdır.")]
    [Display(Name = "Miktar")]
    public decimal Quantity { get; set; } = 1m;

    [ValidateNever]
    public IReadOnlyList<SelectListItem> PortfolioOptions { get; set; } = [];

    [ValidateNever]
    public IReadOnlyList<StockSymbol> Symbols { get; set; } = [];
}
