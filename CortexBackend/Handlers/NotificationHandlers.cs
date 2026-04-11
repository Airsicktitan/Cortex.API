using Cortex.API.Services;

namespace Cortex.API.Handlers;

public static class NotificationHandlers
{
    public static async Task<IResult> GetNotifications(
        int? take,
        IUserContextService userContext,
        INotificationService notificationService)
    {
        var currentUser = await userContext.GetCurrentUserAsync();
        var feed = await notificationService.GetFeedAsync(currentUser.Id, take ?? 20);
        return Results.Ok(feed);
    }

    public static async Task<IResult> MarkNotificationRead(
        int id,
        IUserContextService userContext,
        INotificationService notificationService)
    {
        var currentUser = await userContext.GetCurrentUserAsync();
        var notification = await notificationService.MarkAsReadAsync(currentUser.Id, id);
        return notification is null ? Results.NotFound() : Results.Ok(notification);
    }

    public static async Task<IResult> MarkAllNotificationsRead(
        IUserContextService userContext,
        INotificationService notificationService)
    {
        var currentUser = await userContext.GetCurrentUserAsync();
        await notificationService.MarkAllAsReadAsync(currentUser.Id);
        return Results.NoContent();
    }
}
