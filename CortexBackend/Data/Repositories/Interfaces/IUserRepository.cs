using Cortex.API.Models;

namespace Cortex.API.Data;

public interface IUserRepository
{
    public Task<IEnumerable<User>> GetAllUsersAsync();
    public Task<User> CreateUserAsync(User user);
    public Task<User?> GetByAuth0IdAsync(string auth0Id);
    public Task<User?> GetByEmailAsync(string email);
    public Task<User?> GetByIdAsync(int id);
    public Task<IEnumerable<User>> GetOnlineUsersAsync(DateTime cutoffUtc, DateTime utcNow);
    public Task UpdateUserAsync(User user);
        

    Task SaveChangesAsync();
}
