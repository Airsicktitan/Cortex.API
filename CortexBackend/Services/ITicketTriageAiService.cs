using Cortex.API.DTO;

namespace Cortex.API.Services;

public interface ITicketTriageAiService
{
    /// <summary>
    /// Generates advisory triage from ticket context. Returns <see cref="TicketTriageGenerateResponse.Unavailable"/>
    /// when the provider is not configured or on non-fatal failures.
    /// </summary>
    Task<TicketTriageGenerateResponse> GenerateTriageAsync(
        TicketTriageInput input,
        CancellationToken cancellationToken = default);
}

/// <summary>Snapshot of ticket fields sent to the model (no navigation entities).</summary>
public sealed class TicketTriageInput
{
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required string CurrentPriority { get; init; }
    public required string Status { get; init; }
    public string? Department { get; init; }
    public required string BoardName { get; init; }

    /// <summary>Enabled statuses and SLA priorities from Cortex — the only allowed vocabulary for recommendations.</summary>
    public required TicketTriageVocabularySnapshot Vocabulary { get; init; }
}
