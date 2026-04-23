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
            ? "High risk signal."
            : string.Equals(aiRisk, "Medium", StringComparison.OrdinalIgnoreCase)
                ? "Moderate risk signal."
                : "Low risk signal.";

        var assignmentLead = decisionType switch
        {
            "KeepCurrentOwner" => $"Final owner: {winner.DisplayName}.",
            "RecommendRebalance" => $"Recommended owner: {winner.DisplayName}.",
            _ => $"Assigned owner: {winner.DisplayName}."
        };

        if (current is not null
            && !string.Equals(current.UserId, winner.UserId, StringComparison.OrdinalIgnoreCase))
        {
            return $"{aiRiskLead} {assignmentLead} Current owner {current.DisplayName} has higher workload pressure.";
        }

        return $"{aiRiskLead} {assignmentLead} Routing fit and workload balance support this recommendation.";
    }
}
