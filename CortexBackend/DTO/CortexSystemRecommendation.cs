namespace Cortex.API.DTO;

public sealed class CortexSystemRecommendation
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "RoutingRule";
    public string SourceType { get; set; } = string.Empty;
    public string SourceId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public string Confidence { get; set; } = "Low";
    public string Severity { get; set; } = "Medium";
    public string Status { get; set; } = "Open";
    public string ActionLabel { get; set; } = string.Empty;
    public string ActionPreview { get; set; } = string.Empty;
    public string? DismissedReason { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public List<string> SupportingFacts { get; set; } = [];
}
