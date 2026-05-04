using Cortex.API.DTO;

namespace Cortex.API.Services;

/// <summary>
/// Tier 11 read-only intake learning aggregates (no writes; existing tables only).
/// </summary>
public interface IIntakeLearningService
{
    Task<IntakeLearningOverviewDto> GetOverviewAsync(CancellationToken cancellationToken = default);
}
