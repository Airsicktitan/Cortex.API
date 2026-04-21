using Cortex.API.DTO;
using Cortex.API.Models;

namespace Cortex.API.Services;

public interface IReassignmentRecommendationService
{
    Task<ReassignmentRecommendationResponse> EvaluateAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, ReassignmentRecommendationResponse>> EvaluateBatchAsync(
        IEnumerable<Ticket> tickets,
        CancellationToken cancellationToken = default);
}
