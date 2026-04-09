namespace Cortex.API.Handlers;

using Cortex.API.Models;
using Cortex.API.DTO;
using Cortex.API.Data;
using Cortex.API.Services;
using System.Security.Claims;

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

    private static bool HasPermission(ClaimsPrincipal? principal, string permission)
    {
        if (principal is null)
        {
            return false;
        }

        return principal.Claims.Any(claim =>
            string.Equals(claim.Type, "permissions", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(claim.Value, permission, StringComparison.OrdinalIgnoreCase));
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

    public static async Task<IResult> GetOnlineUsers(
        IUserRepository repo,
        ISessionConfigurationService sessionConfigurationService)
    {
        var configuration = await sessionConfigurationService.GetAsync();
        var utcNow = DateTime.UtcNow;
        var cutoffUtc = utcNow.AddMinutes(-configuration.InactivityTimeoutMinutes);
        var users = await repo.GetOnlineUsersAsync(cutoffUtc, utcNow);

        return Results.Ok(users.Select(user => user.ToOnlineResponse()).ToList());
    }

    public static async Task<IResult> UpdateCurrentUserPresence(
        IUserContextService userContext,
        IUserRepository repo)
    {
        var user = await userContext.GetCurrentUserAsync();
        user.LastSeenDateUtc = DateTime.UtcNow;
        await repo.SaveChangesAsync();

        return Results.NoContent();
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

    public static async Task<IResult> CreateUser(
        CreateUserRequest request,
        IUserRepository repo,
        IUserContextService userContext,
        IAuth0ManagementService auth0ManagementService,
        IHttpContextAccessor httpContextAccessor)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName) ||
            string.IsNullOrWhiteSpace(request.Email) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["displayName"] = ["Display name is required."],
                ["email"] = ["Email is required."],
                ["password"] = ["Password is required."]
            });
        }

        if (request.Password.Trim().Length < 8)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["password"] = ["Password must be at least 8 characters long."]
            });
        }

        if (string.IsNullOrWhiteSpace(request.Role) ||
            !Enum.TryParse<UserRole>(request.Role.Trim(), ignoreCase: true, out var role))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["role"] = ["A valid role is required."]
            });
        }

        var normalizedEmail = request.Email.Trim();
        var existingUser = await repo.GetByEmailAsync(normalizedEmail);
        if (existingUser is not null)
        {
            return Results.Conflict(new { message = "A user with this email already exists." });
        }

        var currentUser = await userContext.GetCurrentUserAsync();
        var principal = httpContextAccessor.HttpContext?.User;
        var isAdminCreator =
            currentUser.Role == UserRole.Admin ||
            HasPermission(principal, "admin:system");

        if (!isAdminCreator && role == UserRole.Admin)
        {
            return Results.Forbid();
        }

        string? createdAuth0UserId = null;

        try
        {
            createdAuth0UserId = await auth0ManagementService.CreateUserAsync(request);

            var user = new User
            {
                DisplayName = request.DisplayName.Trim(),
                NickName = NormalizeOptionalValue(request.NickName),
                Email = normalizedEmail,
                PhoneNumber = NormalizeOptionalValue(request.PhoneNumber),
                Department = NormalizeOptionalValue(request.Department),
                Role = role,
                IsActive = request.IsActive,
                ExpiryDate = request.ExpiryDate,
                Auth0Id = createdAuth0UserId,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow
            };

            await repo.CreateUserAsync(user);
            await repo.SaveChangesAsync();

            var createdUser = await repo.GetByIdAsync(user.Id) ?? user;
            return Results.Created($"/api/users/{createdUser.Id}", createdUser.ToAdminResponse());
        }
        catch (Auth0ManagementException exception)
        {
            return Results.Problem(
                title: "Failed to provision user in Auth0",
                detail: exception.Message,
                statusCode: exception.StatusCode switch
                {
                    400 => StatusCodes.Status400BadRequest,
                    401 or 403 => StatusCodes.Status502BadGateway,
                    409 => StatusCodes.Status409Conflict,
                    _ => StatusCodes.Status502BadGateway
                });
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(
                title: "Auth0 management is not configured",
                detail: exception.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
        catch
        {
            if (!string.IsNullOrWhiteSpace(createdAuth0UserId))
            {
                try
                {
                    await auth0ManagementService.DeleteUserAsync(createdAuth0UserId);
                }
                catch
                {
                    // Best-effort rollback only.
                }
            }

            throw;
        }
    }
}
