using BorsaAnaliz.Web.Models;

namespace BorsaAnaliz.Web.ViewModels;

public sealed record NewsIndexViewModel(
    string ActiveTab,
    IReadOnlyList<KapDisclosure> Disclosures)
{
    public bool IsAll => ActiveTab == "tum";
    public bool IsBuybacks => ActiveTab == "geri-alimlar";
    public bool IsDividends => ActiveTab == "temettu";
    public bool IsCapitalIncreases => ActiveTab == "sermaye-artirimlari";
}
