namespace Cortex.API.DTO;

/// <summary>Stored on <see cref="Models.Ticket.AiScreenshotInsightJson"/> and returned on <see cref="TicketResponse"/> after successful analysis.</summary>
public sealed class ScreenshotInsightPersistedDto
{
    public const string SourceMarker = "screenshot_insight";

    /// <summary>Always <see cref="SourceMarker"/> for rows written by Cortex.</summary>
    public string Source { get; set; } = SourceMarker;

    public DateTime AnalyzedAtUtc { get; set; }

    public int AnalyzedImageCount { get; set; }

    public List<string> AnalyzedFileNames { get; set; } = [];

    public string Summary { get; set; } = "";

    public List<string> VisibleDetails { get; set; } = [];

    public List<string> PossibleIssues { get; set; } = [];

    public List<string> RecommendedFollowUp { get; set; } = [];
}
