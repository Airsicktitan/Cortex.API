using Cortex.API.DTO;
using Cortex.API.Services;

namespace Cortex.API.Handlers;

/// <summary>
/// Read-only + advisory handlers for Recurring Issue Intelligence.
/// Grouping is fully deterministic; the AI review endpoint is advisory only.
/// </summary>
public static class RepeatIssueHandlers
{
    private const int DefaultTopN = 8;
    private const int MaxSampleTickets = 8;

    /// <summary>GET /api/metrics/repeat-issues?topN={n}</summary>
    public static async Task<IResult> GetOverview(
        IRepeatIssueAnalyticsService analytics,
        int? topN,
        CancellationToken cancellationToken)
    {
        var overview = await analytics.GetOverviewAsync(topN ?? DefaultTopN, cancellationToken);
        return Results.Ok(overview);
    }

    /// <summary>GET /api/metrics/repeat-issues/{groupKey}</summary>
    public static async Task<IResult> GetGroupDetail(
        IRepeatIssueAnalyticsService analytics,
        string groupKey,
        CancellationToken cancellationToken)
    {
        var detail = await analytics.GetGroupDetailAsync(groupKey, cancellationToken);
        return detail is null ? Results.NotFound() : Results.Ok(detail);
    }

    /// <summary>POST /api/metrics/repeat-issues/{groupKey}/ai-review</summary>
    public static async Task<IResult> GenerateAiReview(
        IRepeatIssueAnalyticsService analytics,
        IRepeatIssueAiReviewService reviewer,
        string groupKey,
        CancellationToken cancellationToken)
    {
        var detail = await analytics.GetGroupDetailAsync(groupKey, cancellationToken);
        if (detail is null)
        {
            return Results.NotFound();
        }

        var samples = detail.Tickets
            .Take(MaxSampleTickets)
            .Select(ticket => new RepeatIssueAiReviewSampleTicket
            {
                Title = ticket.Title,
                Status = ticket.Status,
                Priority = ticket.Priority,
                CreatedDate = ticket.CreatedDate,
                ResolutionHours = ticket.ResolutionHours,
                CommentCount = ticket.CommentCount,
            })
            .ToList();

        var input = new RepeatIssueAiReviewInput
        {
            GroupKey = detail.Summary.GroupKey,
            RepresentativeTitle = detail.Summary.RepresentativeTitle,
            BoardName = detail.Summary.BoardName,
            SignatureTokens = detail.Summary.SignatureTokens,
            RepeatCount = detail.Summary.RepeatCount,
            OpenCount = detail.Summary.OpenCount,
            FirstSeenUtc = detail.Summary.FirstSeenUtc,
            LastSeenUtc = detail.Summary.LastSeenUtc,
            AvgResolutionHours = detail.Summary.AvgResolutionHours,
            TotalResolutionHours = detail.Summary.TotalResolutionHours,
            OperationalTouchCount = detail.Summary.OperationalTouchCount,
            TrendDelta = detail.Summary.TrendDelta,
            TrendLabel = detail.Summary.TrendLabel,
            DominantPriority = detail.DominantPriority,
            DominantStatus = detail.DominantStatus,
            SampleTickets = samples,
        };

        var result = await reviewer.GenerateReviewAsync(input, cancellationToken);
        return Results.Ok(result);
    }
}
