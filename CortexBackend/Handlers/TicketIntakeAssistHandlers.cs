namespace Cortex.API.Handlers;

using Cortex.API.DTO;
using Cortex.API.Services;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// User-facing Improve Request intake assist. This is intentionally stateless:
/// the handler does not read, create, or mutate tickets, and nothing it returns is persisted.
/// The requester is the caller — this is not reviewer triage.
/// </summary>
public static class TicketIntakeAssistHandlers
{
    private const int MaxTitleLength = 200;
    private const int MaxDescriptionLength = 4000;
    private const int MaxBoardNameLength = 120;

    /// <summary>Log category anchor so request logs group under this feature, not generic ticket handlers.</summary>
    public sealed class IntakeAssistLogCategory { }

    /// <summary>
    /// POST /api/tickets/intake-assist
    /// Accepts the requester's draft title/description, returns clarity coaching.
    /// Returns 200 with Unavailable=true when AI is misconfigured or fails (never blocks submit).
    /// </summary>
    public static async Task<IResult> ImproveIntake(
        IntakeAssistRequest? request,
        ITicketIntakeAssistAiService intakeAssistAi,
        IWorkflowMetricsService metrics,
        ILogger<IntakeAssistLogCategory> logger,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.BadRequest(new { message = "Request body is required." });
        }

        var title = (request.Title ?? string.Empty).Trim();
        var description = (request.Description ?? string.Empty).Trim();
        var boardName = request.BoardName?.Trim();
        var ticketIdMetric = string.IsNullOrWhiteSpace(request.TicketId) ? null : request.TicketId.Trim();
        var clientFlowRaw = (request.ClientFlow ?? string.Empty).Trim().ToLowerInvariant();
        var clientFlow = clientFlowRaw is "create" or "edit" ? clientFlowRaw : "unknown";

        await metrics.TryRecordAsync(
            "intake_assist_requested",
            new
            {
                clientFlow,
                descriptionPresent = description.Length > 0,
            },
            ticketIdMetric,
            cancellationToken);

        if (description.Length == 0)
        {
            return Results.BadRequest(new
            {
                message = "Add a description before improving the request.",
            });
        }

        if (title.Length > MaxTitleLength)
        {
            return Results.BadRequest(new
            {
                message = $"Title must be {MaxTitleLength} characters or fewer.",
            });
        }

        if (description.Length > MaxDescriptionLength)
        {
            return Results.BadRequest(new
            {
                message = $"Description must be {MaxDescriptionLength} characters or fewer.",
            });
        }

        if (!string.IsNullOrEmpty(boardName) && boardName.Length > MaxBoardNameLength)
        {
            // Background context only — clamp quietly rather than fail the request.
            boardName = boardName[..MaxBoardNameLength];
        }

        var input = new IntakeAssistInput
        {
            Title = title,
            Description = description,
            BoardName = string.IsNullOrWhiteSpace(boardName) ? null : boardName,
        };

        try
        {
            var result = await intakeAssistAi.ImproveAsync(input, cancellationToken);
            await metrics.TryRecordAsync(
                "intake_assist_completed",
                new
                {
                    clarityState = result.ClarityState,
                    missingDetailCount = result.MissingDetails.Count,
                    suggestedSummaryReturned = !string.IsNullOrWhiteSpace(result.SuggestedSummary),
                    improvedDescriptionReturned = !string.IsNullOrWhiteSpace(result.ImprovedDescription),
                    unavailable = result.Unavailable,
                },
                ticketIdMetric,
                cancellationToken);
            return Results.Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Intake assist failed unexpectedly. TitleLength={TitleLength} DescriptionLength={DescriptionLength}",
                title.Length,
                description.Length);

            var fallback = new IntakeAssistResponse
            {
                Unavailable = true,
                UnavailableReason = "Improve Request is unavailable right now. Try again in a moment.",
                MissingDetails = [],
            };
            await metrics.TryRecordAsync(
                "intake_assist_completed",
                new
                {
                    clarityState = (string?)null,
                    missingDetailCount = 0,
                    suggestedSummaryReturned = false,
                    improvedDescriptionReturned = false,
                    unavailable = true,
                },
                ticketIdMetric,
                cancellationToken);
            return Results.Ok(fallback);
        }
    }
}
