using Cortex.API.DTO;
using Cortex.API.Handlers;

namespace Cortex.API.Extensions;

public static class NotificationChannelConfigurationEndpoints
{
    public static void MapNotificationChannelConfigurationEndpoints(this WebApplication app)
    {
        var settings = app.MapGroup("/api/settings/notification-channels")
            .RequireAuthorization("SlaManage")
            .WithTags("Notification Channel Configuration");

        settings.MapGet("/", NotificationChannelConfigurationHandlers.GetNotificationChannelConfiguration)
            .WithName("GetNotificationChannelConfiguration")
            .Produces<NotificationChannelConfigurationResponse>(StatusCodes.Status200OK);

        settings.MapPut("/", NotificationChannelConfigurationHandlers.UpdateNotificationChannelConfiguration)
            .WithName("UpdateNotificationChannelConfiguration")
            .Produces<NotificationChannelConfigurationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);
    }
}
