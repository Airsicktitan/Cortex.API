using Cortex.API.DTO;
using Cortex.API.Models;

namespace Cortex.API.Services;

public interface INotificationService
{
    Task<NotificationFeedResponse> GetFeedAsync(int userId, int take = 20);
    Task<NotificationResponse?> MarkAsReadAsync(int userId, int notificationId);
    Task MarkAllAsReadAsync(int userId);
    Task<int> CreateAssignmentNotificationsAsync(Ticket originalTicket, Ticket updatedTicket, User actor);
    Task<int> CreateAssignmentNotificationsForNewTicketAsync(Ticket ticket, User actor);
    Task<int> CreateCommentNotificationsAsync(Ticket ticket, User actor);
    Task<int> CreateArchiveNotificationsAsync(Ticket ticket, User actor, bool ticketIsArchived, bool isReactivated = false);
    Task<int> ProcessSlaNotificationsAsync(DateTime utcNow, CancellationToken cancellationToken = default);
}
