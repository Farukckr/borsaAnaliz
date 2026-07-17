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
    public const string DividendSubject = "Kar Payı Dağıtım İşlemlerine İlişkin Bildirim";
    public const string CapitalIncreaseSubject = "Sermaye Artırımı - Azaltımı İşlemlerine İlişkin Bildirim";
    public const string CapitalIncreaseAlternativeSubject = "Sermaye Artırımı veya Azaltımı Bildirimi";

    public bool IsBuyback => Subject.Equals(BuybackSubject, StringComparison.OrdinalIgnoreCase);
    public bool IsDividend => Subject.Equals(DividendSubject, StringComparison.OrdinalIgnoreCase);
    public bool IsCapitalIncrease =>
        Subject.Equals(CapitalIncreaseSubject, StringComparison.OrdinalIgnoreCase) ||
        Subject.Equals(CapitalIncreaseAlternativeSubject, StringComparison.OrdinalIgnoreCase);

    public bool IsEvent(KapDisclosureEventKind eventKind) => eventKind switch
    {
        KapDisclosureEventKind.Buyback => IsBuyback,
        KapDisclosureEventKind.Dividend => IsDividend,
        KapDisclosureEventKind.CapitalIncrease => IsCapitalIncrease,
        _ => false
    };

    public string SourceUrl => $"https://www.kap.org.tr/tr/Bildirim/{Id}";
}

public enum KapDisclosureEventKind
{
    Buyback,
    Dividend,
    CapitalIncrease
}
