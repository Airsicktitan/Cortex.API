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

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}