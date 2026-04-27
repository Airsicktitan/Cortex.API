namespace Cortex.API.Models;

public sealed class CortexSystemRecommendationState
{
    public int Id { get; set; }
    public string RecommendationId { get; set; } = string.Empty;
    public string Status { get; set; } = "Open";
    public string? DismissedReason { get; set; }
    public int? ReviewedBy { get; set; }
    public DateTime? ReviewedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastUpdatedAtUtc { get; set; }
}
