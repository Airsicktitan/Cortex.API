using Cortex.API.DTO;

namespace Cortex.API.Services;

/// <summary>
/// User-facing Improve Request assist. Strictly clarity-focused and stateless:
/// does not assign priority, status, or SLA, and does not persist any result.
/// Returns <see cref="IntakeAssistResponse.Unavailable"/> when the provider is not configured
/// or on non-fatal failures so the submit flow is never blocked.
/// </summary>
public interface ITicketIntakeAssistAiService
{
    Task<IntakeAssistResponse> ImproveAsync(
        IntakeAssistInput input,
        CancellationToken cancellationToken = default);
}

/// <summary>Snapshot of the requester's draft inputs (no ticket/user entities).</summary>
public sealed class IntakeAssistInput
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public string? BoardName { get; init; }
}
