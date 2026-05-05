namespace Cortex.API.Authorization;

/// <summary>
/// Cortex RBAC permission identifiers documented for product alignment.
/// Enforcement is via Auth0 roles + ASP.NET policies (<see cref="CortexAuthorizationExtensions"/>).
/// </summary>
public static class CortexProductPermissions
{
    public const string ApprovalQueueView = "approval.queue.view";
    public const string ApprovalTicketView = "approval.ticket.view";
    public const string ApprovalTicketApprove = "approval.ticket.approve";
    public const string ApprovalTicketRequestMoreInfo = "approval.ticket.request_more_info";
    public const string ApprovalTicketReject = "approval.ticket.reject";
    public const string ApprovalTicketComment = "approval.ticket.comment";
    public const string CortexDecisionView = "cortex.decision.view";
    public const string CortexRiskView = "cortex.risk.view";
    public const string CortexIntakeView = "cortex.intake.view";
    public const string CortexEvidenceView = "cortex.evidence.view";
    public const string CortexHistoryView = "cortex.history.view";
}
