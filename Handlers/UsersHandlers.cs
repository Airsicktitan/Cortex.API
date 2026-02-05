namespace Cortex.API.Handlers;

using Cortex.API.Models;
using Cortex.API.Database;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Defines all user-related API handlers for CORTEX.
/// Implements RESTful CRUD operations with database persistence via Entity Framework Core.
///  Known Limitations:
/// - POST endpoint uses client-side ID generation (see inline TODO)
/// - Authentication/authorization not yet implemented (see inline TODOs)
/// 
/// Future Enhancements:
/// - JWT authentication
/// - Role-based authorization (admin vs regular user)
/// - Audit logging for all operations
/// - Input validation middleware
/// </summary>

public static class UserHandlers
{
    public static async Task<IResult> GetUsersTest(CortexDbContext db)
    {
        var users = await db.Users.ToListAsync();
        return Results.Ok(users);
    }
    public static async Task<IResult> CreateUserTest(User user, CortexDbContext db)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return Results.Ok(new
        {
            message = "User created successfully",
            userId = user.Id,
            user
        });
    }
    
}
