namespace BorsaAnaliz.Web.Models;

public sealed record KapDisclosure(
    long Id,
    DateTimeOffset PublishedAt,
    string CompanyName,
    IReadOnlyList<string> StockCodes,
    string? MatchedSymbol,
    string Category,
    string Type,
    string Subject,
    string? Summary)
{
    public const string BuybackSubject = "Payların Geri Alınmasına İlişkin Bildirim";

    public bool IsBuyback => Subject.Equals(BuybackSubject, StringComparison.OrdinalIgnoreCase);

    public string SourceUrl => $"https://www.kap.org.tr/tr/Bildirim/{Id}";
}
