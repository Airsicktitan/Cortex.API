using Cortex.API.Models;

namespace Cortex.API.Services;

public interface INotificationChannelConfigurationService
{
    Task<NotificationChannelConfiguration> GetAsync();
    Task<NotificationChannelConfiguration> SaveAsync(NotificationChannelConfiguration configuration);
}
