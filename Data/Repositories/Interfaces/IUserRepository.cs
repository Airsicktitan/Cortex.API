using Cortex.API.Models;

namespace Cortex.API.Data;

public interface IUserRepository
{
    public Task<IEnumerable<User>> GetAllUsersAsync();
    public Task<User> CreateUserAsync(User user);
    public Task<User?> GetByAuth0IdAsync(string auth0Id);

    Task SaveChangesAsync();
}