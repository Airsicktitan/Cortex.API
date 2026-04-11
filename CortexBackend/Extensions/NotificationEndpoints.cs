using Cortex.API.DTO;
using Cortex.API.Handlers;

namespace Cortex.API.Extensions;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var notifications = app.MapGroup("/api/notifications")
            .RequireAuthorization()
            .WithTags("Notifications");

        notifications.MapGet("/", NotificationHandlers.GetNotifications)
            .WithName("GetNotifications")
            .Produces<NotificationFeedResponse>(StatusCodes.Status200OK);

        notifications.MapPost("/{id:int}/read", NotificationHandlers.MarkNotificationRead)
            .WithName("MarkNotificationRead")
            .Produces<NotificationResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        notifications.MapPost("/read-all", NotificationHandlers.MarkAllNotificationsRead)
            .WithName("MarkAllNotificationsRead")
            .Produces(StatusCodes.Status204NoContent);
    }
}
