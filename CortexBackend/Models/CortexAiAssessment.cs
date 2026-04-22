namespace Cortex.API.Models;

/// <summary>
/// Unified, vocabulary-constrained AI intake assessment (advisory only; never persisted by the AI layer).
/// </summary>
public sealed class CortexAiAssessment
{
    public string Summary { get; set; } = "";

    public string RecommendedPriority { get; set; } = "";
    public string RecommendedStatus { get; set; } = "";
    public string RecommendedCategory { get; set; } = "";

    public string? RecommendedOwnerUserId { get; set; }

    public string RiskLevel { get; set; } = "";
    public decimal ConfidenceScore { get; set; }

    public List<string> Reasons { get; set; } = [];
    public List<string> MissingInformation { get; set; } = [];

    public List<string> Evidence { get; set; } = [];
}
