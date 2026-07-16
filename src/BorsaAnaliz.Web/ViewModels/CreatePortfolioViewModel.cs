using System.ComponentModel.DataAnnotations;

namespace BorsaAnaliz.Web.ViewModels;

public sealed class CreatePortfolioViewModel
{
    [Required(ErrorMessage = "Portföy adı gereklidir.")]
    [StringLength(100, ErrorMessage = "Portföy adı en fazla 100 karakter olabilir.")]
    [Display(Name = "Portföy adı")]
    public string Name { get; set; } = string.Empty;

    [Range(typeof(decimal), "1", "1000000000", ErrorMessage = "Başlangıç nakdi sıfırdan büyük olmalıdır.")]
    [Display(Name = "Başlangıç nakdi")]
    public decimal InitialCash { get; set; } = 100_000m;
}
