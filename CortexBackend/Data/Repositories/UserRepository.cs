using Cortex.API.Database;
using Cortex.API.Models;

using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Data.Repositories;

public class UserRepository(CortexDbContext context) : IUserRepository
{
    private readonly CortexDbContext _context = context;

    public async Task<IEnumerable<User>> GetAllUsersAsync()
    {
        return await _context.Users.ToListAsync();
    }
    
    public async Task<User> CreateUserAsync(User user)
    {
        await _context.Users.AddAsync(user);
        return user;
    }

    public async Task<User?> GetByAuth0IdAsync(string auth0Id)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Auth0Id == auth0Id);
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await _context.Users.FirstOrDefaultAsync(
            u => u.Email.ToLower() == normalizedEmail);
    }

    public async Task<User?> GetByIdAsync(int id)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<IEnumerable<User>> GetOnlineUsersAsync(DateTime cutoffUtc, DateTime utcNow)
    {
        return await _context.Users
            .Where(user =>
                user.IsActive &&
                user.LastSeenDateUtc != null &&
                user.LastSeenDateUtc >= cutoffUtc &&
                (user.ExpiryDate == null || user.ExpiryDate > utcNow))
            .OrderByDescending(user => user.LastSeenDateUtc)
            .ThenBy(user => user.DisplayName ?? user.Email)
            .ToListAsync();
    }

    public async Task UpdateUserAsync(User user)
    {
        _context.Users.Update(user);
        await _context.SaveChangesAsync();
    }
    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
