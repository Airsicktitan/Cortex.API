using Cortex.API.DTO;
using Cortex.API.Models;

namespace Cortex.API.Services;

public interface IOperationalRiskService
{
    Task<OperationalRiskResponse> EvaluateAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, OperationalRiskResponse>> EvaluateBatchAsync(
        IEnumerable<Ticket> tickets,
        CancellationToken cancellationToken = default);
}
