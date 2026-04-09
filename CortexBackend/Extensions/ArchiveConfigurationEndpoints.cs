using Cortex.API.Handlers;

namespace Cortex.API.Extensions;

public static class ArchiveConfigurationEndpoints
{
    public static void MapArchiveConfigurationEndpoints(this WebApplication app)
    {
        var archive = app.MapGroup("/api/settings/archive")
            .RequireAuthorization("SlaManage")
            .WithTags("Archive");

        archive.MapGet("/", ArchiveConfigurationHandlers.GetArchiveConfigurations)
            .WithName("GetArchiveConfigurations")
            .Produces(StatusCodes.Status200OK);

        archive.MapPost("/", ArchiveConfigurationHandlers.CreateArchiveConfiguration)
            .WithName("CreateArchiveConfiguration")
            .Produces(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);

        archive.MapPut("/{id:int}", ArchiveConfigurationHandlers.UpdateArchiveConfiguration)
            .WithName("UpdateArchiveConfiguration")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .Produces(StatusCodes.Status400BadRequest);

        archive.MapDelete("/{id:int}", ArchiveConfigurationHandlers.DeleteArchiveConfiguration)
            .WithName("DeleteArchiveConfiguration")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status404NotFound);

        archive.MapPost("/run", ArchiveConfigurationHandlers.RunArchiveNow)
            .WithName("RunArchiveNow")
            .Produces(StatusCodes.Status200OK);
    }
}
