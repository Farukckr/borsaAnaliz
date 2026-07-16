namespace BorsaAnaliz.Web.Services;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    public string Provider { get; set; } = "Gemini";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gemini-3.5-flash";
}
