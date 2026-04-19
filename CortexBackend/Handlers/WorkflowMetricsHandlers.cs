using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Services;

namespace Cortex.API.Handlers;

/// <summary>Append-only workflow instrumentation (no UI).</summary>
public static class WorkflowMetricsHandlers
{
    /// <summary>POST /api/tickets/{id}/metrics/reviewer-quality-signal</summary>
    public static async Task<IResult> RecordReviewerQualitySignalShown(
        string id,
        ReviewerQualitySignalMetricsRequest? request,
        ITicketRepository ticketRepository,
        ICommentRepository commentRepository,
        ITicketVisibilityService ticketVisibilityService,
        IWorkflowMetricsService metrics,
        CancellationToken cancellationToken)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.ReviewerSignal))
        {
            return Results.BadRequest(new { message = "Reviewer signal is required." });
        }

        var ticket = await ticketRepository.GetTicketByIdAsync(id);
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        var commentCount = await commentRepository.CountCommentsByTicketIdAsync(id);

        await metrics.TryRecordAsync(
            "reviewer_quality_signal_shown",
            new
            {
                reviewerSignal = request.ReviewerSignal.Trim(),
                missingDetailHintCount = request.MissingDetailHintCount,
                commentCount,
            },
            id,
            cancellationToken);

        return Results.NoContent();
    }
}
