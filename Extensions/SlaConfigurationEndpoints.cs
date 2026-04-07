using Cortex.API.Handlers;

namespace Cortex.API.Extensions;

public static class SlaConfigurationEndpoints
{
    public static void MapSlaConfigurationEndpoints(this WebApplication app)
    {
        var sla = app.MapGroup("/api/settings/sla")
            .RequireAuthorization("SlaManage")
            .WithTags("SLA");

        sla.MapGet("/", SlaConfigurationHandlers.GetSlaConfiguration)
            .WithName("GetSlaConfiguration")
            .Produces(StatusCodes.Status200OK);

        sla.MapPut("/", SlaConfigurationHandlers.UpdateSlaConfiguration)
            .WithName("UpdateSlaConfiguration")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);
    }
}
