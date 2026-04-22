using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Cortex.API.Handlers;

public static class AiHandlers
{
    public sealed class AiHandlersLogCategory { }

    /// <summary>Unified AI intake assessment (advisory; does not persist).</summary>
    public static async Task<Results<Ok<CortexAiAssessment>, BadRequest<string>, NotFound>> PostAssessTicket(
        AiAssessRequest? body,
        [FromServices] ITicketRepository ticketRepository,
        [FromServices] ITicketVisibilityService ticketVisibilityService,
        [FromServices] ICortexAiAssessmentService cortexAiAssessmentService,
        ILogger<AiHandlersLogCategory> logger,
        CancellationToken cancellationToken)
    {
        if (body is null)
        {
            return TypedResults.BadRequest("Request body is required.");
        }

        if (string.IsNullOrWhiteSpace(body.TicketId) && body.Ticket is null)
        {
            return TypedResults.BadRequest("Provide ticketId or a ticket payload.");
        }

        if (body.Ticket is { } p
            && string.IsNullOrWhiteSpace(body.TicketId)
            && string.IsNullOrWhiteSpace(p.Id))
        {
            if (string.IsNullOrWhiteSpace(p.Title) || p.BoardId <= 0)
            {
                return TypedResults.BadRequest("Ticket payload requires title and a valid boardId when ticketId is omitted.");
            }
        }

        var ticket = await ResolveTicketForAssessmentAsync(body, ticketRepository, cancellationToken);
        if (ticket is null)
        {
            return TypedResults.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return TypedResults.NotFound();
        }

        try
        {
            var assessment = await cortexAiAssessmentService.AssessTicketAsync(ticket, cancellationToken);
            return TypedResults.Ok(assessment);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "POST /api/ai/assess failed for ticket id {TicketId}", ticket.Id);
            return TypedResults.Ok(
                new CortexAiAssessment
                {
                    Summary = "AI assessment could not be completed. Defaults were returned.",
                    RecommendedPriority = ticket.Priority,
                    RecommendedStatus = ticket.Status,
                    RecommendedCategory = string.Empty,
                    RecommendedOwnerUserId = null,
                    RiskLevel = "Low",
                    ConfidenceScore = 0.15m,
                    Reasons = ["An unexpected error occurred while building the assessment."],
                    MissingInformation = [],
                    Evidence = [],
                });
        }
    }

    private static async Task<Ticket?> ResolveTicketForAssessmentAsync(
        AiAssessRequest body,
        ITicketRepository ticketRepository,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(body.TicketId))
        {
            return await ticketRepository.GetTicketByIdAsync(body.TicketId.Trim());
        }

        if (body.Ticket is null)
        {
            return null;
        }

        var payload = body.Ticket;
        if (!string.IsNullOrWhiteSpace(payload.Id))
        {
            var existing = await ticketRepository.GetTicketByIdAsync(payload.Id.Trim());
            if (existing is not null)
            {
                return MergePayloadOntoTicket(existing, payload);
            }
        }

        return BuildSyntheticTicket(payload);
    }

    private static Ticket MergePayloadOntoTicket(Ticket existing, AiAssessTicketPayload payload)
    {
        var merged = new Ticket
        {
            Id = existing.Id,
            Title = string.IsNullOrWhiteSpace(payload.Title) ? existing.Title : payload.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(payload.Description)
                ? existing.Description
                : payload.Description,
            Priority = string.IsNullOrWhiteSpace(payload.Priority) ? existing.Priority : payload.Priority.Trim(),
            Status = string.IsNullOrWhiteSpace(payload.Status) ? existing.Status : payload.Status.Trim(),
            BoardId = payload.BoardId != 0 ? payload.BoardId : existing.BoardId,
            CreatedBy = existing.CreatedBy,
            SynitiOwner = existing.SynitiOwner,
            BusinessOwner = existing.BusinessOwner,
            ApprovalStatus = existing.ApprovalStatus,
            AiScreenshotInsightJson = payload.AiScreenshotInsightJson ?? existing.AiScreenshotInsightJson,
            Comments = payload.Comments is null ? [] : MapCommentPayloads(payload.Comments, existing.Id),
        };

        return merged;
    }

    private static Ticket BuildSyntheticTicket(AiAssessTicketPayload payload)
    {
        return new Ticket
        {
            Id = string.Empty,
            Title = payload.Title.Trim(),
            Description = payload.Description ?? string.Empty,
            Priority = string.IsNullOrWhiteSpace(payload.Priority) ? "Medium" : payload.Priority.Trim(),
            Status = string.IsNullOrWhiteSpace(payload.Status) ? "New" : payload.Status.Trim(),
            BoardId = payload.BoardId,
            CreatedBy = 0,
            AiScreenshotInsightJson = payload.AiScreenshotInsightJson,
            Comments = MapCommentPayloads(payload.Comments, string.Empty),
        };
    }

    private static List<Comment> MapCommentPayloads(
        List<AiAssessCommentPayload>? payloads,
        string ticketId)
    {
        if (payloads is null || payloads.Count == 0)
        {
            return [];
        }

        return payloads
            .Select(p => new Comment
            {
                TicketId = ticketId,
                Body = p.Body ?? string.Empty,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow,
            })
            .Where(c => !string.IsNullOrWhiteSpace(c.Body))
            .ToList();
    }
}
