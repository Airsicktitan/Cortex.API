using Cortex.API.Models;

namespace Cortex.API.Services;

public interface ICortexSlaRiskService
{
    Task<CortexSlaRiskAssessment> EvaluateRiskAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default);
}
