using BorsaAnaliz.Web.Models;

namespace BorsaAnaliz.Web.Services;

public interface IKapNewsService
{
    Task<IReadOnlyList<KapDisclosure>> GetLatestAsync(
        CancellationToken cancellationToken = default);
}
