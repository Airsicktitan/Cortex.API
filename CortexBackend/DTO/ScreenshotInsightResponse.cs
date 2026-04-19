namespace Cortex.API.DTO;

/// <summary>Structured screenshot insight for approvers. Advisory only; successful responses are persisted on the ticket as JSON.</summary>
public sealed class ScreenshotInsightResponse
{
    public string Summary { get; set; } = "";

    public List<string> VisibleDetails { get; set; } = [];

    public List<string> PossibleIssues { get; set; } = [];

    public List<string> RecommendedFollowUp { get; set; } = [];

    /// <summary>True when OpenAI is not configured or the call failed; UI shows a soft message.</summary>
    public bool Unavailable { get; set; }

    public string? UnavailableReason { get; set; }
}
