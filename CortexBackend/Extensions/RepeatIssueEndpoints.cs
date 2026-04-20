using Cortex.API.Authorization;
using Cortex.API.Configuration;
using Cortex.API.DTO;
using Cortex.API.Handlers;

namespace Cortex.API.Extensions;

/// <summary>
/// Endpoints for Recurring Issue Intelligence.
/// Mounted under /api/metrics to align with the existing metrics surface.
/// </summary>
public static class RepeatIssueEndpoints
{
    public static void MapRepeatIssueEndpoints(this WebApplication app)
    {
        var repeatIssues = app.MapGroup("/api/metrics/repeat-issues")
            .RequireAuthorization(CortexAuthorizationExtensions.BusinessAccess)
            .WithTags("Metrics");

        repeatIssues.MapGet("/", RepeatIssueHandlers.GetOverview)
            .WithName("GetRepeatIssueOverview")
            .Produces<RepeatIssueOverviewResponse>(StatusCodes.Status200OK);

        repeatIssues.MapGet("/{groupKey}", RepeatIssueHandlers.GetGroupDetail)
            .WithName("GetRepeatIssueGroupDetail")
            .Produces<RepeatIssueGroupDetailResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        repeatIssues.MapPost("/{groupKey}/ai-review", RepeatIssueHandlers.GenerateAiReview)
            .RequireRateLimiting(AiRateLimitPolicies.StandardPolicyName)
            .WithName("GenerateRepeatIssueAiReview")
            .Produces<RepeatIssueAiReviewResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status429TooManyRequests);
    }
}
