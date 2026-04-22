using Cortex.API.Models;

namespace Cortex.API.Services;

public interface ICortexDecisionService
{
    Task<CortexDecisionResult> EvaluateAssignmentAsync(
        Ticket ticket,
        CortexAiAssessment? aiAssessment = null,
        CancellationToken cancellationToken = default);
    Task<CortexDecisionResult> EvaluateRebalanceAsync(Ticket ticket, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RebalanceSuggestion>> GetRebalanceSuggestionsAsync(CancellationToken cancellationToken = default);
    Task<ExecuteRebalanceResponse> ExecuteRebalanceAsync(CancellationToken cancellationToken = default);
}
