using Cortex.API.Models;

namespace Cortex.API.Services;

public interface ICortexAiAssessmentService
{
    /// <summary>
    /// Produces one constrained, advisory assessment by orchestrating existing triage + persisted vision signals.
    /// Does not persist to the database.
    /// </summary>
    Task<CortexAiAssessment> AssessTicketAsync(Ticket ticket, CancellationToken cancellationToken = default);
}
