using Cortex.API.Authorization;
using Cortex.API.Configuration;
using Cortex.API.DTO;
using Cortex.API.Handlers;
using Cortex.API.Models;

namespace Cortex.API.Extensions;

public static class AiEndpoints
{
    public static void MapAiEndpoints(this WebApplication app)
    {
        var ai = app.MapGroup("/api/ai")
            .RequireAuthorization()
            .WithTags("AI");

        ai.MapPost("/assess", AiHandlers.PostAssessTicket)
            .RequireAuthorization(CortexAuthorizationExtensions.BusinessDataAccess)
            .RequireRateLimiting(AiRateLimitPolicies.StandardPolicyName)
            .WithName("PostCortexAiAssess")
            .Accepts<AiAssessRequest>("application/json")
            .Produces<CortexAiAssessment>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);
    }
}
