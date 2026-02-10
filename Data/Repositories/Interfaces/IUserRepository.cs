using Cortex.API.Models;

namespace Cortex.API.Data;

public interface IUserRepository
{
    public Task<IEnumerable<User>> GetAllUsersAsync();
    public Task<User> CreateUserAsync(User user);

    Task SaveChangesAsync();
}