using Cortex.API.Handlers;

namespace Cortex.API.Extensions;

public static class SessionConfigurationEndpoints
{
    public static void MapSessionConfigurationEndpoints(this WebApplication app)
    {
        var session = app.MapGroup("/api/settings/session")
            .WithTags("Session");

        session.MapGet("/", SessionConfigurationHandlers.GetSessionConfiguration)
            .RequireAuthorization()
            .WithName("GetSessionConfiguration")
            .Produces(StatusCodes.Status200OK);

        session.MapPut("/", SessionConfigurationHandlers.UpdateSessionConfiguration)
            .RequireAuthorization("SlaManage")
            .WithName("UpdateSessionConfiguration")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);
    }
}
