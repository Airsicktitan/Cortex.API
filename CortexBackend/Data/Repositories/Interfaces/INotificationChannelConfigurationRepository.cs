using Cortex.API.Models;

namespace Cortex.API.Data.Repositories;

public interface INotificationChannelConfigurationRepository
{
    Task<NotificationChannelConfiguration?> GetAsync();
    Task UpsertAsync(NotificationChannelConfiguration configuration);
    Task SaveChangesAsync();
}
