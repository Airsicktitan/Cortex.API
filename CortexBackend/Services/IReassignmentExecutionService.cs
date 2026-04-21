using Cortex.API.DTO;
using Cortex.API.Models;

namespace Cortex.API.Services;

public interface IReassignmentExecutionService
{
    Task<ReassignmentExecutionResult> ExecuteAsync(
        Ticket ticket,
        ReassignmentApplyRequest request,
        User actor,
        CancellationToken cancellationToken = default);
}

public sealed record ReassignmentExecutionResult(
    bool Succeeded,
    int StatusCode,
    string Message,
    string? PreviousOwner,
    string? NewOwner,
    string AssignmentField,
    string ReassignmentSource,
    DecisionImpactSnapshot? DecisionImpactSnapshot);

public sealed record DecisionImpactSnapshot(
    int? PreviousOwnerId,
    string AssignmentField,
    int PreviousOwnerWorkload,
    string PreviousPressureLevel,
    string PreviousRiskLevel,
    string? PreviousSlaStatus,
    DateTime AppliedAtUtc,
    string Source);
