using Cortex.API.Models;

namespace Cortex.API.Services;

public interface INotificationDeliveryService
{
    Task DeliverAsync(
        NotificationChannelMode mode,
        IReadOnlyList<UserNotification> notifications,
        IReadOnlyDictionary<int, User> recipientsById,
        CancellationToken cancellationToken = default);
}
