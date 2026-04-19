using System.Text.Json.Serialization;

namespace Cortex.API.DTO;

/// <summary>Raw JSON contract returned by the AI triage model.</summary>
public sealed class TicketTriageAiResponse
{
    [JsonPropertyName("summary")]
    public string? Summary { get; set; }

    [JsonPropertyName("priority")]
    public string? Priority { get; set; }

    [JsonPropertyName("priorityReason")]
    public string? PriorityReason { get; set; }

    [JsonPropertyName("status")]
    public string? Status { get; set; }

    [JsonPropertyName("missingDetails")]
    public List<string>? MissingDetails { get; set; }

    [JsonPropertyName("potentialSlaRisk")]
    public string? PotentialSlaRisk { get; set; }

    [JsonPropertyName("slaRiskReason")]
    public string? SlaRiskReason { get; set; }
}
