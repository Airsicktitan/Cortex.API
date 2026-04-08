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
    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    public static async Task<IResult> GetUsers(IUserRepository repo)
    {
        var users = await repo.GetAllUsersAsync();
        var response = users
            .OrderBy(user => user.DisplayName ?? user.Email)
            .Select(user => user.ToAdminResponse())
            .ToList();

        return Results.Ok(response);
    }

    public static async Task<IResult> GetCurrentUser(IUserContextService userContext)
    {
        var user = await userContext.GetCurrentUserAsync();
        
        return Results.Ok(user.ToResponse());
    }

    public static async Task<IResult> UpdateUserProfile(IUserContextService userContext, UpdateUserProfileRequest request)
    {
        var user = await userContext.GetCurrentUserAsync();

        await userContext.UpdateProfileAsync(user, request);

        return Results.Ok(user.ToResponse());
    }

    public static async Task<IResult> UpdateUser(
        int id,
        AdminUpdateUserRequest request,
        IUserRepository repo)
    {
        var user = await repo.GetByIdAsync(id);
        if (user is null)
        {
            return Results.NotFound($"User {id} was not found.");
        }

        if (string.IsNullOrWhiteSpace(request.Role) ||
            !Enum.TryParse<UserRole>(request.Role.Trim(), ignoreCase: true, out var role))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["role"] = ["A valid role is required."]
            });
        }

        user.NickName = NormalizeOptionalValue(request.NickName);
        user.PhoneNumber = NormalizeOptionalValue(request.PhoneNumber);
        user.Department = NormalizeOptionalValue(request.Department);
        user.Role = role;
        user.IsActive = request.IsActive;
        user.ExpiryDate = request.ExpiryDate;
        user.LastModifiedDate = DateTime.UtcNow;

        await repo.SaveChangesAsync();

        return Results.Ok(user.ToAdminResponse());
    }
}
