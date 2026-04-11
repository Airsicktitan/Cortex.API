using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Data.Repositories;

public class NotificationRepository(CortexDbContext context) : INotificationRepository
{
    private readonly CortexDbContext _context = context;

    public async Task<IReadOnlyList<UserNotification>> GetRecentByUserIdAsync(int userId, int take)
    {
        return await _context.UserNotifications
            .Where(notification => notification.UserId == userId)
            .OrderByDescending(notification => notification.CreatedDateUtc)
            .Take(take)
            .ToListAsync();
    }

    public async Task<IReadOnlyList<UserNotification>> GetUnreadByUserIdAsync(int userId)
    {
        return await _context.UserNotifications
            .Where(notification => notification.UserId == userId && !notification.IsRead)
            .OrderByDescending(notification => notification.CreatedDateUtc)
            .ToListAsync();
    }

    public async Task<int> GetUnreadCountAsync(int userId)
    {
        return await _context.UserNotifications.CountAsync(notification =>
            notification.UserId == userId &&
            !notification.IsRead);
    }

    public async Task<UserNotification?> GetByIdAsync(int id)
    {
        return await _context.UserNotifications.FirstOrDefaultAsync(notification => notification.Id == id);
    }

    public async Task<bool> ExistsAsync(int userId, string deduplicationKey)
    {
        return await _context.UserNotifications.AnyAsync(notification =>
            notification.UserId == userId &&
            notification.DeduplicationKey == deduplicationKey);
    }

    public async Task AddAsync(UserNotification notification)
    {
        await _context.UserNotifications.AddAsync(notification);
    }

    public async Task AddRangeAsync(IEnumerable<UserNotification> notifications)
    {
        await _context.UserNotifications.AddRangeAsync(notifications);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
