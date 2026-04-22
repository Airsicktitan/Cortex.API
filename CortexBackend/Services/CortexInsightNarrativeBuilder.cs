using Cortex.API.Models;

namespace Cortex.API.Services;

public static class CortexInsightNarrativeBuilder
{
    public static string BuildCortexInsightSummary(
        CortexAiAssessment? aiAssessment,
        CortexDecisionCandidate winner,
        CortexDecisionCandidate? current,
        string decisionType)
    {
        var aiRisk = aiAssessment?.RiskLevel?.Trim();
        var aiRiskLead = string.Equals(aiRisk, "High", StringComparison.OrdinalIgnoreCase)
            ? "High-risk ticket detected."
            : string.Equals(aiRisk, "Medium", StringComparison.OrdinalIgnoreCase)
                ? "Moderate-risk ticket detected."
                : "Ticket risk remains manageable.";

        var assignmentLead = decisionType switch
        {
            "KeepCurrentOwner" => $"Kept with {winner.DisplayName}",
            "RecommendRebalance" => $"Recommend moving to {winner.DisplayName}",
            _ => $"Assigned to {winner.DisplayName}"
        };

        if (current is not null
            && !string.Equals(current.UserId, winner.UserId, StringComparison.OrdinalIgnoreCase))
        {
            return $"{aiRiskLead} {assignmentLead} to avoid overload on {current.DisplayName} and reduce SLA exposure.";
        }

        return $"{aiRiskLead} {assignmentLead} based on routing fit and workload balance.";
    }
}
