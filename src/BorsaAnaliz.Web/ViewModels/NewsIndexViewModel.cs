using BorsaAnaliz.Web.Models;

namespace BorsaAnaliz.Web.ViewModels;

public sealed record NewsIndexViewModel(
    string ActiveTab,
    IReadOnlyList<KapDisclosure> Disclosures)
{
    public bool IsBuybacks => ActiveTab == "geri-alimlar";
}
