using System.Text.Json.Serialization;

namespace Cortex.API.DTO;

/// <summary>Aggregated workflow assist usage (v1 snapshot for Reports).</summary>
public sealed class WorkflowMetricsSnapshotResponse
{
    public int IntakeAssistUsageCount { get; set; }

    public int IntakeAssistSavedCount { get; set; }

    /// <summary>Average of missingDetailCount from intake_assist_completed events with valid values.</summary>
    public double AvgMissingDetailCount { get; set; }

    public ReviewerSignalCountsDto ReviewerSignalCounts { get; set; } = new();

    public int ScreenshotInsightUsageCount { get; set; }

    public AvgCommentCountBySignalDto AvgCommentCountBySignal { get; set; } = new();
}

public sealed class ReviewerSignalCountsDto
{
    public int Ready { get; set; }
    public int Gaps { get; set; }

    [JsonPropertyName("needs_detail")]
    public int NeedsDetail { get; set; }
}

public sealed class AvgCommentCountBySignalDto
{
    public double Ready { get; set; }
    public double Gaps { get; set; }

    [JsonPropertyName("needs_detail")]
    public double NeedsDetail { get; set; }
}
