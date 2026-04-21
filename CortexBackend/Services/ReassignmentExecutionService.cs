using Cortex.API.DTO;
using Cortex.API.Data;
using Cortex.API.Models;

namespace Cortex.API.Services;

public sealed class ReassignmentExecutionService(
    IReassignmentRecommendationService reassignmentRecommendationService,
    IUserRepository userRepository,
    IOperationalRiskService operationalRiskService,
    ISlaConfigurationService slaConfigurationService) : IReassignmentExecutionService
{
    private readonly IReassignmentRecommendationService _reassignmentRecommendationService = reassignmentRecommendationService;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IOperationalRiskService _operationalRiskService = operationalRiskService;
    private readonly ISlaConfigurationService _slaConfigurationService = slaConfigurationService;

    public async Task<ReassignmentExecutionResult> ExecuteAsync(
        Ticket ticket,
        ReassignmentApplyRequest request,
        User actor,
        CancellationToken cancellationToken = default)
    {
        if (request.SelectedOwnerId <= 0)
        {
            return Failed(StatusCodes.Status400BadRequest, "Selected owner is required.");
        }

        if (ticket.ApprovalStatus != ApprovalStatus.Approved)
        {
            return Failed(StatusCodes.Status409Conflict, "Ticket must be approved before reassignment.");
        }

        var recommendation = await _reassignmentRecommendationService.EvaluateAsync(ticket, cancellationToken);
        if (!recommendation.ShouldSuggestReassignment)
        {
            return Failed(
                StatusCodes.Status409Conflict,
                "This recommendation is no longer valid because assignment conditions changed.");
        }

        var assignmentField = recommendation.AssignmentField;
        if (assignmentField is not "synitiOwner" and not "businessOwner")
        {
            return Failed(StatusCodes.Status400BadRequest, "Ticket is missing a valid owner assignment target.");
        }

        var currentOwner = assignmentField == "synitiOwner"
            ? Normalize(ticket.SynitiOwner)
            : Normalize(ticket.BusinessOwner);
        if (currentOwner.Length == 0)
        {
            return Failed(StatusCodes.Status409Conflict, "Current owner is no longer set on this ticket.");
        }

        var expectedCurrentOwner = Normalize(request.ExpectedCurrentOwnerKey);
        if (expectedCurrentOwner.Length > 0
            && !string.Equals(expectedCurrentOwner, currentOwner, StringComparison.OrdinalIgnoreCase))
        {
            return Failed(
                StatusCodes.Status409Conflict,
                "Ticket assignment changed before reassignment could be applied.");
        }

        var selectedTarget = recommendation.SuggestedTargets.FirstOrDefault(target =>
            target.UserId.HasValue && target.UserId.Value == request.SelectedOwnerId);
        if (selectedTarget is null)
        {
            return Failed(StatusCodes.Status400BadRequest, "Suggested target is no longer eligible for this ticket.");
        }

        var selectedUser = await _userRepository.GetByIdAsync(request.SelectedOwnerId);
        if (selectedUser is null)
        {
            return Failed(StatusCodes.Status400BadRequest, "Selected owner no longer exists.");
        }

        var newOwner = Normalize(selectedUser.DisplayName);
        if (newOwner.Length == 0)
        {
            newOwner = Normalize(selectedUser.Email);
        }
        if (newOwner.Length == 0)
        {
            return Failed(StatusCodes.Status400BadRequest, "Selected owner does not have a valid identity label.");
        }

        if (string.Equals(newOwner, currentOwner, StringComparison.OrdinalIgnoreCase))
        {
            return Failed(StatusCodes.Status409Conflict, "Ticket is already assigned to the selected owner.");
        }

        var now = DateTime.UtcNow;
        var previousRisk = await _operationalRiskService.EvaluateAsync(ticket, cancellationToken);
        var slaConfigurations = await _slaConfigurationService.GetPriorityMapAsync();
        slaConfigurations.TryGetValue(ticket.Priority ?? string.Empty, out var slaConfiguration);
        var slaSnapshot = TicketSlaCalculator.Calculate(ticket, slaConfiguration);
        var source = Normalize(request.Source).Length > 0
            ? Normalize(request.Source)
            : "cortex_recommendation_review";
        var impactSnapshot = new DecisionImpactSnapshot(
            PreviousOwnerId: recommendation.CurrentOwner?.UserId,
            AssignmentField: assignmentField,
            PreviousOwnerWorkload: recommendation.CurrentOwner?.WorkloadScore ?? 0,
            PreviousPressureLevel: Normalize(recommendation.CurrentOwner?.PressureLevel).Length > 0
                ? Normalize(recommendation.CurrentOwner?.PressureLevel)
                : "low",
            PreviousRiskLevel: Normalize(previousRisk.RiskLevel).Length > 0
                ? Normalize(previousRisk.RiskLevel)
                : "low",
            PreviousSlaStatus: slaSnapshot.Status,
            AppliedAtUtc: now,
            Source: source);

        if (assignmentField == "synitiOwner")
        {
            ticket.SynitiOwner = newOwner;
        }
        else
        {
            ticket.BusinessOwner = newOwner;
        }

        ticket.LastModifiedBy = actor.Id;
        ticket.LastModifiedDate = now;

        return new ReassignmentExecutionResult(
            Succeeded: true,
            StatusCode: StatusCodes.Status200OK,
            Message: "Reassignment applied.",
            PreviousOwner: currentOwner,
            NewOwner: newOwner,
            AssignmentField: assignmentField,
            ReassignmentSource: source,
            DecisionImpactSnapshot: impactSnapshot);
    }

    private static ReassignmentExecutionResult Failed(int statusCode, string message)
    {
        return new ReassignmentExecutionResult(
            Succeeded: false,
            StatusCode: statusCode,
            Message: message,
            PreviousOwner: null,
            NewOwner: null,
            AssignmentField: "unassigned",
            ReassignmentSource: "cortex_recommendation_review",
            DecisionImpactSnapshot: null);
    }

    private static string Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}
