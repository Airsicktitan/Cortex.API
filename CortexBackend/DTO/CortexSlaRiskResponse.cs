namespace Cortex.API.DTO;

public class CortexSlaRiskResponse
{
    public string TicketId { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = "Low";
    public List<string> RiskReasons { get; set; } = [];
    public string Recommendation { get; set; } = "Keep on current path";
    public string RecommendationReason { get; set; } = string.Empty;
    public decimal Confidence { get; set; }
    public string SlaStatus { get; set; } = string.Empty;
    public DateTime EvaluatedAtUtc { get; set; } = DateTime.UtcNow;
}
