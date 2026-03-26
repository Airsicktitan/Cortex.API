namespace Cortex.API.Handlers;

using Cortex.API.Models;
using Cortex.API.DTO;
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
        
        if (!users.Any())
            return Results.NotFound("No users found.");
        
        var response = users.Select(u => u.ToResponse());

        return Results.Ok(response.ToList());
    }

    public static async Task<IResult> GetCurrentUser(IUserContextService userContext)
    {
        // Extract Auth0 user ID from JWT claims
        var user = await userContext.GetCurrentUserAsync();

        if(string.IsNullOrWhiteSpace(user.DisplayName))
            return Results.Ok(new
            {
                requiresProfileCompletion = true,
                userId = user.Id
            });

        return Results.Ok(user.ToResponse());
    }

    public static async Task<IResult> UpdateUserProfile(IUserContextService userContext, UpdateUserProfileRequest request)
    {
        var user = await userContext.GetCurrentUserAsync();

        await userContext.UpdateProfileAsync(user, request);

        return Results.Ok(user.ToResponse());
    }
}