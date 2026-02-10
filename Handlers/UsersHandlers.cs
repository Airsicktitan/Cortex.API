namespace Cortex.API.Handlers;

using Cortex.API.Models;
using Cortex.API.DTOs;

using Microsoft.AspNetCore.Identity;
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
            Username = user.Username,
            Email = user.Email,
            Department = user.Department,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            CreatedDate = user.CreatedDate,
            LastLoginDate = user.LastLoginDate
        });

        return Results.Ok(response.ToList());

    }
    public static async Task<IResult> CreateUser(CreateUserRequest request, IUserRepository repo)
    {
        var hasher = new PasswordHasher<User>();
        
        var user = new User
        {
            Username = request.Username,
            Email = request.Email,
            PasswordHash = string.Empty, // Will be set after hashing
            Department = request.Department,
            Role = UserRole.User, // Default role
            IsActive = true,
            CreatedDate = DateTime.UtcNow
        };

        user.PasswordHash = hasher.HashPassword(user, request.Password);

        await repo.CreateUserAsync(user);
        await repo.SaveChangesAsync();

        var response = new UserResponse
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Department = user.Department,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            CreatedDate = user.CreatedDate,
            LastLoginDate = user.LastLoginDate
        };

        return Results.Created($"/api/users/{user.Id}", response);
    }
    
}
