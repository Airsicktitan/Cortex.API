using Cortex.API.Models;

namespace Cortex.API.Data.Repositories;

public interface INotificationRepository
{
    Task<IReadOnlyList<UserNotification>> GetRecentByUserIdAsync(int userId, int take);
    Task<IReadOnlyList<UserNotification>> GetUnreadByUserIdAsync(int userId);
    Task<int> GetUnreadCountAsync(int userId);
    Task<UserNotification?> GetByIdAsync(int id);
    Task<bool> ExistsAsync(int userId, string deduplicationKey);
    Task AddAsync(UserNotification notification);
    Task AddRangeAsync(IEnumerable<UserNotification> notifications);
    Task SaveChangesAsync();
}
