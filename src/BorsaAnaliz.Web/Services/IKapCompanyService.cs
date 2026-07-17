using BorsaAnaliz.Web.Models;

namespace BorsaAnaliz.Web.Services;

public interface IKapCompanyService
{
    Task<KapCompanyProfile?> GetCompanyProfileAsync(
        string symbol,
        CancellationToken cancellationToken = default);
}
