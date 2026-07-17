using BorsaAnaliz.Web.Models;

namespace BorsaAnaliz.Web.Services;

public interface IKapNewsService
{
    Task<IReadOnlyList<KapDisclosure>> GetLatestAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KapDisclosure>> GetBuybacksAsync(
        int days = 14,
        CancellationToken cancellationToken = default);

    Task<KapDisclosureResult> GetForSymbolAsync(
        string symbol,
        CancellationToken cancellationToken = default);
}

public sealed record KapDisclosureResult(
    bool IsAvailable,
    IReadOnlyList<KapDisclosure> Disclosures);
