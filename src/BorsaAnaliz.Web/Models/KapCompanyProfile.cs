namespace BorsaAnaliz.Web.Models;

public sealed record KapCompanyProfile(
    IReadOnlyList<KapOwnershipRow> Ownership,
    IReadOnlyList<KapSubsidiary> Subsidiaries);

public sealed record KapOwnershipRow(
    string Holder,
    decimal SharePercentage);

public sealed record KapSubsidiary(
    string Name,
    string? Activity,
    decimal? SharePercentage);
