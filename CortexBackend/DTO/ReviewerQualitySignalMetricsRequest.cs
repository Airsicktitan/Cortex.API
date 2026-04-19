namespace Cortex.API.DTO;

/// <summary>Reviewer intake quality band when the signal is shown (metrics only).</summary>
public sealed class ReviewerQualitySignalMetricsRequest
{
    /// <summary>ready | gaps | needs_detail | none</summary>
    public required string ReviewerSignal { get; set; }

    public int? MissingDetailHintCount { get; set; }
}
