using Cortex.API.Handlers;

namespace Cortex.API.Extensions;

public static class ArchiveConfigurationEndpoints
{
    public static void MapArchiveConfigurationEndpoints(this WebApplication app)
    {
        var archive = app.MapGroup("/api/settings/archive")
            .RequireAuthorization("SlaManage")
            .WithTags("Archive");

        archive.MapGet("/", ArchiveConfigurationHandlers.GetArchiveConfiguration)
            .WithName("GetArchiveConfiguration")
            .Produces(StatusCodes.Status200OK);

        archive.MapPut("/", ArchiveConfigurationHandlers.UpdateArchiveConfiguration)
            .WithName("UpdateArchiveConfiguration")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        archive.MapPost("/run", ArchiveConfigurationHandlers.RunArchiveNow)
            .WithName("RunArchiveNow")
            .Produces(StatusCodes.Status200OK);
    }
}
