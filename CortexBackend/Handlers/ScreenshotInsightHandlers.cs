using System.Text.Json;
using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;

namespace Cortex.API.Handlers;

/// <summary>Approver-facing screenshot analysis. Persists the latest successful insight on the ticket (advisory JSON).</summary>
public static class ScreenshotInsightHandlers
{
    private const long MaxBytesPerImage = 8 * 1024 * 1024;

    private static readonly JsonSerializerOptions PersistJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public sealed class ScreenshotInsightLogCategory { }

    /// <summary>POST /api/tickets/{ticketId}/attachments/screenshot-insight</summary>
    public static async Task<IResult> AnalyzeScreenshotAttachments(
        string ticketId,
        ITicketRepository ticketRepository,
        ITicketAttachmentRepository attachmentRepository,
        ICommentRepository commentRepository,
        ITicketVisibilityService ticketVisibilityService,
        IAiSettingsService aiSettingsService,
        IScreenshotInsightAiService insightAi,
        IWorkflowMetricsService metrics,
        ILogger<ScreenshotInsightLogCategory> logger,
        CancellationToken cancellationToken)
    {
        var ticket = await ticketRepository.GetTicketByIdAsync(ticketId);
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        var aiSettings = await aiSettingsService.GetAsync();
        var all = (await attachmentRepository.GetByTicketIdAsync(ticketId)).ToList();
        var images = new List<(string FileName, string ContentType, byte[] Content)>();

        foreach (var attachment in all.OrderByDescending(a => a.UploadedDate).ThenByDescending(a => a.Id))
        {
            if (!IsEligibleImage(attachment))
            {
                continue;
            }

            if (attachment.Content.Length > MaxBytesPerImage)
            {
                logger.LogInformation(
                    "Skipping oversized image for screenshot insight. AttachmentId={AttachmentId} Bytes={Bytes}",
                    attachment.Id,
                    attachment.Content.Length);
                continue;
            }

            images.Add((attachment.FileName, attachment.ContentType, attachment.Content));
            if (images.Count >= aiSettings.MaxScreenshotAttachmentCount)
            {
                break;
            }
        }

        var commentCount = await commentRepository.CountCommentsByTicketIdAsync(ticketId);
        await metrics.TryRecordAsync(
            "screenshot_insight_requested",
            new
            {
                imageCount = images.Count,
                commentCount,
            },
            ticketId,
            cancellationToken);

        if (images.Count == 0)
        {
            return Results.BadRequest(new
            {
                message = "No supported image attachments found. Use PNG, JPG, JPEG, or WEBP.",
            });
        }

        try
        {
            var result = await insightAi.AnalyzeAsync(ticket.Title ?? "", images, cancellationToken);
            await metrics.TryRecordAsync(
                "screenshot_insight_completed",
                new
                {
                    visibleDetailCount = result.VisibleDetails.Count,
                    possibleIssuesCount = result.PossibleIssues.Count,
                    recommendedFollowUpCount = result.RecommendedFollowUp.Count,
                    unavailable = result.Unavailable,
                    commentCount,
                },
                ticketId,
                cancellationToken);

            if (!result.Unavailable)
            {
                var fileNames = images.ConvertAll(static i => i.FileName);
                ticket.AiScreenshotInsightJson = JsonSerializer.Serialize(
                    new ScreenshotInsightPersistedDto
                    {
                        Source = ScreenshotInsightPersistedDto.SourceMarker,
                        AnalyzedAtUtc = DateTime.UtcNow,
                        AnalyzedImageCount = images.Count,
                        AnalyzedFileNames = fileNames,
                        Summary = result.Summary ?? "",
                        VisibleDetails = result.VisibleDetails ?? [],
                        PossibleIssues = result.PossibleIssues ?? [],
                        RecommendedFollowUp = result.RecommendedFollowUp ?? [],
                    },
                    PersistJsonOptions);
                await ticketRepository.UpdateTicketAsync(ticket);
                await ticketRepository.SaveChangesAsync();
            }

            return Results.Ok(result);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Screenshot insight failed unexpectedly. TicketId={TicketId}", ticketId);
            var fallback = new ScreenshotInsightResponse
            {
                Unavailable = true,
                UnavailableReason = "Unable to analyze screenshots right now. Try again later.",
            };
            await metrics.TryRecordAsync(
                "screenshot_insight_completed",
                new
                {
                    visibleDetailCount = 0,
                    possibleIssuesCount = 0,
                    recommendedFollowUpCount = 0,
                    unavailable = true,
                    commentCount,
                },
                ticketId,
                cancellationToken);
            return Results.Ok(fallback);
        }
    }

    private static bool IsEligibleImage(TicketAttachment attachment)
    {
        if (IsSupportedImageExtension(attachment.FileName))
        {
            return true;
        }

        return IsSupportedImageContentType(attachment.ContentType);
    }

    private static bool IsSupportedImageExtension(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext is ".png" or ".jpg" or ".jpeg" or ".webp";
    }

    private static bool IsSupportedImageContentType(string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType))
        {
            return false;
        }

        var c = contentType.Split(';')[0].Trim().ToLowerInvariant();
        return c is "image/png" or "image/jpeg" or "image/jpg" or "image/webp";
    }
}
