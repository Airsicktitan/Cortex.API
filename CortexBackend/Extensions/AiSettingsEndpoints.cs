using Cortex.API.Authorization;
using Cortex.API.DTO;
using Cortex.API.Handlers;

namespace Cortex.API.Extensions;

public static class AiSettingsEndpoints
{
    public static void MapAiSettingsEndpoints(this WebApplication app)
    {
        var aiSettings = app.MapGroup("/api/settings/ai")
            .RequireAuthorization(CortexAuthorizationExtensions.AdminOnly)
            .WithTags("AI Settings");

        aiSettings.MapGet("/", AiSettingsHandlers.GetAiSettings)
            .WithName("GetAiSettings")
            .Produces<AiSettingsResponse>(StatusCodes.Status200OK);

        aiSettings.MapPut("/", AiSettingsHandlers.UpdateAiSettings)
            .WithName("UpdateAiSettings")
            .Produces<AiSettingsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);
    }
}
