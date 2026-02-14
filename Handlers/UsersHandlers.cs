namespace Cortex.API.Handlers;

using Cortex.API.Models;
using Cortex.API.DTOs;
using Cortex.API.Data;
using Cortex.API.Services;

/// <summary>
/// Defines all user-related API handlers for CORTEX.
/// Implements RESTful CRUD operations with database persistence via Entity Framework Core.
/// </summary>
public static class UserHandlers
{
    public static async Task<IResult> GetUsers(IUserRepository repo)
    {
        var users = await repo.GetAllUsersAsync();
        
        if (users.Count() == 0)
            return Results.NotFound("No users found.");
        
        var response = users.Select(u => u.ToResponse());

        return Results.Ok(response.ToList());
    }

    public static async Task<IResult> GetCurrentUser(HttpContext http, IUserContextService userContext)
    {
        // Extract Auth0 user ID from JWT claims
        var user = await userContext.GetCurrentUserAsync(http.User);

        if(string.IsNullOrWhiteSpace(user.DisplayName))
            return Results.Ok(new
            {
                requiresProfileCompletion = true,
                userId = user.Id
            });

        return Results.Ok(user.ToResponse());
    }

    public static async Task<IResult> UpdateUserProfile(HttpContext http, IUserContextService userContext, IUserRepository repo, UpdateUserProfileRequest request)
    {
        var user = await userContext.GetCurrentUserAsync(http.User);

        if (user == null)
            return Results.Unauthorized();

        // Update only allowed fields
        user.DisplayName = request.DisplayName ?? user.DisplayName;
        user.Department = request.Department ?? user.Department;

        await repo.UpdateUserAsync(user);

        return Results.Ok(user.ToResponse());
    }
}