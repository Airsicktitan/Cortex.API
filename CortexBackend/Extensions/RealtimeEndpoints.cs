using Cortex.API.Handlers;

namespace Cortex.API.Extensions;

public static class RealtimeEndpoints
{
    public static void MapRealtimeEndpoints(this WebApplication app)
    {
        app.MapGet("/api/realtime/stream", RealtimeHandlers.StreamEvents)
            .RequireAuthorization()
            .WithTags("Realtime");
    }
}
