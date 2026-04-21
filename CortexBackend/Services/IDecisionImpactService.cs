using Cortex.API.DTO;
using Cortex.API.Models;

namespace Cortex.API.Services;

public interface IDecisionImpactService
{
    Task<DecisionImpactResponse?> EvaluateAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, DecisionImpactResponse>> EvaluateBatchAsync(
        IEnumerable<Ticket> tickets,
        CancellationToken cancellationToken = default);
}
