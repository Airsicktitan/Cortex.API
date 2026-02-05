namespace Cortex.API.Extensions;

using Cortex.API.Models;
using Cortex.API.Database;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Defines all ticket-related API endpoints for CORTEX.
/// Implements RESTful CRUD operations with database persistence via Entity Framework Core.
/// 
/// Known Limitations:
/// - POST endpoint uses client-side ID generation (see inline TODO)
/// - Authentication/authorization not yet implemented (see inline TODOs)
/// - Delete endpoint commented out pending auth implementation
/// 
/// Future Enhancements:
/// - JWT authentication
/// - Role-based authorization (admin vs regular user)
/// - Audit logging for all operations
/// - Input validation middleware
/// </summary>

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        // User endpoints can be added here similarly
        var users = app.MapGroup("/api/users")
            .WithTags("Users");

        users.MapPost("/test", async (User user, CortexDbContext db) =>
        {
            db.Users.Add(user);
            await db.SaveChangesAsync();
            return Results.Ok(new
            {
                message = "User created successfully",
                userId = user.Id,
                user
            });
        })
        .WithName("CreateUserTest")
        .Produces<User>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest);

        users.MapGet("/test", async (CortexDbContext db) =>
        {
            var users = await db.Users.ToListAsync();
            return Results.Ok(users);
        })
        .WithName("GetUsersTest")
        .Produces<List<User>>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest);;
    }
}
