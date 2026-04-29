namespace Cortex.API.Extensions;

using Cortex.API.Authorization;
using Cortex.API.Configuration;
using Cortex.API.Handlers;
using Cortex.API.DTO;
using Cortex.API.Models;

/// <summary>
/// Ticket API: read for all authenticated roles; writes gated by capability policies.
/// </summary>
public static class TicketEndpoints
{
    public static void MapTicketEndpoints(this WebApplication app)
    {
        var tickets = app.MapGroup("/api/tickets")
            .RequireAuthorization()
            .WithTags("Tickets");

        tickets.MapGet("/archived", TicketHandlers.GetArchivedTickets)
            .WithName("GetArchivedTickets")
            .Produces<PagedArchivedTicketListResponse>(StatusCodes.Status200OK);

        tickets.MapGet("/board-counts", TicketHandlers.GetTicketBoardCounts)
            .WithName("GetTicketBoardCounts")
            .Produces<List<TicketBoardCountResponse>>(StatusCodes.Status200OK);

        tickets.MapGet("/my-submissions", TicketHandlers.GetTicketsByUser)
            .WithName("GetMyTicketSubmissions")
            .Produces<List<TicketResponse>>(StatusCodes.Status200OK);

        tickets.MapGet("/pending-approval", TicketHandlers.GetTicketsPendingApproval)
            .RequireAuthorization(CortexAuthorizationExtensions.BusinessDataAccess)
            .WithName("GetTicketsPendingApproval")
            .Produces<PagedTicketListResponse>(StatusCodes.Status200OK);

        tickets.MapGet("/", TicketHandlers.GetAllTickets)
            .WithName("GetAllTickets")
            .Produces<PagedTicketListResponse>(StatusCodes.Status200OK);

        tickets.MapGet("/{id}", TicketHandlers.GetTicketById)
            .WithName("GetTicketById")
            .Produces<TicketResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        tickets.MapPost("/{id}/triage", TicketHandlers.GenerateTicketTriage)
            .RequireAuthorization(CortexAuthorizationExtensions.BusinessDataAccess)
            .RequireRateLimiting(AiRateLimitPolicies.StandardPolicyName)
            .WithName("GenerateTicketTriage")
            .Produces<TicketTriageGenerateResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        tickets.MapPost("/{id}/triage/apply", TicketHandlers.ApplyTicketTriageSuggestions)
            .RequireAuthorization(CortexAuthorizationExtensions.BusinessDataAccess)
            .RequireRateLimiting(AiRateLimitPolicies.StandardPolicyName)
            .WithName("ApplyTicketTriageSuggestions")
            .Accepts<TicketTriageApplyRequest>("application/json")
            .Produces<TicketResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        // User-facing Improve Request: stateless, no persistence, no ticket ID.
        // Gated by StandardWriteAccess so only users who can actually submit tickets can invoke it.
        tickets.MapPost("/intake-assist", TicketIntakeAssistHandlers.ImproveIntake)
            .RequireAuthorization(CortexAuthorizationExtensions.StandardWriteAccess)
            .RequireRateLimiting(AiRateLimitPolicies.StandardPolicyName)
            .WithName("ImproveTicketIntake")
            .Accepts<IntakeAssistRequest>("application/json")
            .Produces<IntakeAssistResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        tickets.MapPost("/{id}/metrics/reviewer-quality-signal", WorkflowMetricsHandlers.RecordReviewerQualitySignalShown)
            .WithName("RecordReviewerQualitySignalMetrics")
            .Accepts<ReviewerQualitySignalMetricsRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        tickets.MapGet("/{id}/history", TicketHandlers.GetTicketHistory)
            .WithName("GetTicketHistory")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        tickets.MapGet("/{id}/routing/latest", TicketHandlers.GetLatestRoutingDecision)
            .WithName("GetLatestTicketRoutingDecision")
            .Produces<TicketRoutingLatestResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        tickets.MapGet("/{id}/decision", TicketHandlers.GetTicketDecision)
            .WithName("GetTicketDecision")
            .Produces<CortexDecisionResult>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        tickets.MapGet("/{id}/autonomy", TicketHandlers.GetTicketAutonomy)
            .WithName("GetTicketAutonomy")
            .Produces<CortexAutonomyResultDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        tickets.MapPost("/{id}/autonomy/evaluate", TicketHandlers.EvaluateTicketAutonomy)
            .RequireAuthorization(CortexAuthorizationExtensions.BusinessDataAccess)
            .RequireRateLimiting(AiRateLimitPolicies.StandardPolicyName)
            .WithName("EvaluateTicketAutonomy")
            .Produces<CortexAutonomyResultDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        tickets.MapGet("/{id}/insight", TicketHandlers.GetTicketInsight)
            .RequireRateLimiting(AiRateLimitPolicies.StandardPolicyName)
            .WithName("GetTicketInsight")
            .Produces<CortexInsightDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status429TooManyRequests);

        tickets.MapGet("/{id}/insight/cache", TicketHandlers.GetTicketCachedInsight)
            .WithName("GetTicketCachedInsight")
            .Produces<CortexInsightDto>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        tickets.MapGet("/{id}/risk", TicketHandlers.GetTicketRisk)
            .WithName("GetTicketRisk")
            .Produces<CortexSlaRiskResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        tickets.MapPost("/routing/workload-preview", TicketHandlers.PostOwnerWorkloadPreview)
            .WithName("PostOwnerWorkloadPreview")
            .Produces<OwnerWorkloadPreviewResponse>(StatusCodes.Status200OK);

        tickets.MapPost("/{id}/reassignment/apply", TicketHandlers.ApplyGuidedReassignment)
            .RequireAuthorization(CortexAuthorizationExtensions.BusinessDataAccess)
            .WithName("ApplyGuidedReassignment")
            .Accepts<ReassignmentApplyRequest>("application/json")
            .Produces<ReassignmentApplyResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status403Forbidden)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        tickets.MapPost("/{id}/approve", TicketHandlers.ApproveTicket)
            .RequireAuthorization(CortexAuthorizationExtensions.BusinessDataAccess)
            .WithName("ApproveTicket")
            .Accepts<TicketApprovalActionRequest>("application/json")
            .Produces<TicketResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        tickets.MapPost("/{id}/return", TicketHandlers.ReturnTicketForDetail)
            .RequireAuthorization(CortexAuthorizationExtensions.BusinessDataAccess)
            .WithName("ReturnTicketForDetail")
            .Accepts<TicketApprovalActionRequest>("application/json")
            .Produces<TicketResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        tickets.MapPost("/{id}/reject", TicketHandlers.RejectTicket)
            .RequireAuthorization(CortexAuthorizationExtensions.BusinessDataAccess)
            .WithName("RejectTicket")
            .Accepts<TicketApprovalActionRequest>("application/json")
            .Produces<TicketResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status409Conflict);

        tickets.MapPost("/{id}/memory-feedback", TicketHandlers.PostMemoryFeedback)
            .WithName("PostMemoryFeedback")
            .Accepts<CortexMemoryFeedbackRequest>("application/json")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest);

        tickets.MapPost("/routing/preview", TicketHandlers.PostRoutingPreview)
            .WithName("PostTicketRoutingPreview")
            .Produces<RoutingPreviewResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        tickets.MapGet("/status/{status}", TicketHandlers.GetTicketsByStatus)
            .WithName("GetTicketsByStatus")
            .Produces<List<TicketResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        tickets.MapGet("/priority/{priority}", TicketHandlers.GetTicketsByPriority)
            .WithName("GetTicketsByPriority")
            .Produces<List<TicketResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        tickets.MapPost("/", TicketHandlers.CreateTicket)
            .RequireAuthorization(CortexAuthorizationExtensions.StandardWriteAccess)
            .WithName("CreateTicket")
            .Produces<TicketResponse>(StatusCodes.Status201Created);

        tickets.MapPut("/{id}", TicketHandlers.UpdateTicket)
            .RequireAuthorization(CortexAuthorizationExtensions.StandardWriteAccess)
            .WithName("UpdateTicket")
            .Produces<TicketResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        tickets.MapPost("/{id}/archive", TicketHandlers.ArchiveTicket)
            .RequireAuthorization(CortexAuthorizationExtensions.BusinessDataAccess)
            .WithName("ArchiveTicket")
            .Accepts<TicketActionReasonRequest>("application/json")
            .Produces<ArchivedTicketResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        tickets.MapPost("/archived/{id}/reactivate", TicketHandlers.ReactivateArchivedTicket)
            .RequireAuthorization(CortexAuthorizationExtensions.BusinessDataAccess)
            .WithName("ReactivateArchivedTicket")
            .Produces<TicketResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status409Conflict);

        tickets.MapDelete("/{id}", TicketHandlers.DeleteTicket)
            .RequireAuthorization(CortexAuthorizationExtensions.BusinessDataAccess)
            .WithName("DeleteTicket")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);
    }
}
