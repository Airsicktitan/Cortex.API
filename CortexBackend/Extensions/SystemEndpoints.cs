using Cortex.API.Authorization;
using Cortex.API.DTO;
using Cortex.API.Database;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Extensions;

public static class SystemEndpoints
{
    public static void MapSystemEndpoints(this WebApplication app)
    {
        var system = app.MapGroup("/api/system")
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .WithTags("System");

        system.MapGet("/recommendations", async (
            ICortexLearningService learningService,
            CancellationToken cancellationToken) =>
        {
            var recommendations = await learningService.GetSystemRecommendationsAsync(cancellationToken);
            return Results.Ok(recommendations);
        })
        .WithName("GetSystemRecommendations")
        .Produces<IReadOnlyList<CortexSystemRecommendation>>(StatusCodes.Status200OK);

        system.MapPost("/recommendations/{id}/accept", async (
            string id,
            CortexDbContext dbContext,
            IUserContextService userContextService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Results.BadRequest(new { message = "Recommendation id is required." });
            }

            var user = await userContextService.GetCurrentUserAsync();
            var state = await dbContext.CortexSystemRecommendationStates
                .FirstOrDefaultAsync(s => s.RecommendationId == id, cancellationToken);

            if (state is null)
            {
                state = new CortexSystemRecommendationState
                {
                    RecommendationId = id.Trim(),
                    Status = "Accepted",
                    ReviewedBy = user.Id,
                    ReviewedAtUtc = DateTime.UtcNow,
                    CreatedAtUtc = DateTime.UtcNow,
                    LastUpdatedAtUtc = DateTime.UtcNow
                };
                dbContext.CortexSystemRecommendationStates.Add(state);
            }
            else
            {
                state.Status = "Accepted";
                state.DismissedReason = null;
                state.ReviewedBy = user.Id;
                state.ReviewedAtUtc = DateTime.UtcNow;
                state.LastUpdatedAtUtc = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok();
        })
        .WithName("AcceptSystemRecommendation")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        system.MapPost("/recommendations/{id}/dismiss", async (
            string id,
            DismissSystemRecommendationRequest request,
            CortexDbContext dbContext,
            IUserContextService userContextService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Results.BadRequest(new { message = "Recommendation id is required." });
            }

            var user = await userContextService.GetCurrentUserAsync();
            var dismissedReason = string.IsNullOrWhiteSpace(request.Reason)
                ? "Dismissed by reviewer."
                : request.Reason.Trim();
            var state = await dbContext.CortexSystemRecommendationStates
                .FirstOrDefaultAsync(s => s.RecommendationId == id, cancellationToken);

            if (state is null)
            {
                state = new CortexSystemRecommendationState
                {
                    RecommendationId = id.Trim(),
                    Status = "Dismissed",
                    DismissedReason = dismissedReason,
                    ReviewedBy = user.Id,
                    ReviewedAtUtc = DateTime.UtcNow,
                    CreatedAtUtc = DateTime.UtcNow,
                    LastUpdatedAtUtc = DateTime.UtcNow
                };
                dbContext.CortexSystemRecommendationStates.Add(state);
            }
            else
            {
                state.Status = "Dismissed";
                state.DismissedReason = dismissedReason;
                state.ReviewedBy = user.Id;
                state.ReviewedAtUtc = DateTime.UtcNow;
                state.LastUpdatedAtUtc = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok();
        })
        .WithName("DismissSystemRecommendation")
        .Accepts<DismissSystemRecommendationRequest>("application/json")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);

        system.MapPost("/recommendations/{id}/defer", async (
            string id,
            CortexDbContext dbContext,
            IUserContextService userContextService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return Results.BadRequest(new { message = "Recommendation id is required." });
            }

            var user = await userContextService.GetCurrentUserAsync();
            var state = await dbContext.CortexSystemRecommendationStates
                .FirstOrDefaultAsync(s => s.RecommendationId == id, cancellationToken);

            if (state is null)
            {
                state = new CortexSystemRecommendationState
                {
                    RecommendationId = id.Trim(),
                    Status = "Deferred",
                    ReviewedBy = user.Id,
                    ReviewedAtUtc = DateTime.UtcNow,
                    CreatedAtUtc = DateTime.UtcNow,
                    LastUpdatedAtUtc = DateTime.UtcNow
                };
                dbContext.CortexSystemRecommendationStates.Add(state);
            }
            else
            {
                state.Status = "Deferred";
                state.DismissedReason = null;
                state.ReviewedBy = user.Id;
                state.ReviewedAtUtc = DateTime.UtcNow;
                state.LastUpdatedAtUtc = DateTime.UtcNow;
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return Results.Ok();
        })
        .WithName("DeferSystemRecommendation")
        .Produces(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);
    }
}

public sealed class DismissSystemRecommendationRequest
{
    public string? Reason { get; set; }
}
