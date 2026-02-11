namespace Cortex.API.Handlers;

using Cortex.API.Models;
using Cortex.API.DTOs;
using Cortex.API.Data;

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
    public static async Task<IResult> GetUsers(IUserRepository repo)
    {
        var users = await repo.GetAllUsersAsync();
        
        if (users.Count() == 0)
            return Results.NotFound("No users found.");
        
        var response = users.Select(user => new UserResponse
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email,
            Department = user.Department,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            CreatedDate = user.CreatedDate,
            LastLoginDate = user.LastLoginDate,
            LastModifiedDate = user.LastModifiedDate
        });

        return Results.Ok(response.ToList());

    }

    public static async Task<IResult> GetCurrentUser(HttpContext http, IUserRepository repo)
    {
        var auth0Id = http.User.FindFirst("sub")?.Value;
        var email = http.User.FindFirst("email")?.Value;
        var name = http.User.FindFirst("name")?.Value;

        if (auth0Id == null)
            return Results.Unauthorized();

        var user = await repo.GetByAuth0IdAsync(auth0Id);

        if (user == null)
        {
            user = new User
            {
                Auth0Id = auth0Id,
                DisplayName = name ?? "Unknown",
                Email = email ?? "",
                CreatedDate = DateTime.UtcNow
            };
            await repo.CreateUserAsync(user);
            await repo.SaveChangesAsync();
        }

        return Results.Ok(new UserResponse
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email,
            Department = user.Department,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            CreatedDate = user.CreatedDate,
            LastLoginDate = user.LastLoginDate,
            LastModifiedDate = user.LastModifiedDate
        });

    }

}
