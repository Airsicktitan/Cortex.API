namespace Cortex.API.Handlers;

using Cortex.API.Models;
using Cortex.API.DTO;
using Cortex.API.Data;
using Cortex.API.Database;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// User-related API handlers. Authorization uses Auth0 roles via ASP.NET policies.
/// </summary>
public static class UserHandlers
{
    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    /// <summary>Parses a single role string for admin user create/update (must match Auth0).</summary>
    private static bool TryParseRole(string? rawRole, out string role) =>
        Auth0Roles.TryNormalize(rawRole, out role);

    private static NotificationChannelMode? ParseNotificationChannelOrNull(
        string? rawValue,
        string fieldName)
    {
        var normalized = NormalizeOptionalValue(rawValue);
        if (normalized is null)
        {
            return null;
        }

        if (Enum.TryParse<NotificationChannelMode>(normalized, true, out var mode) &&
            Enum.IsDefined(mode))
        {
            return mode;
        }

        throw new ArgumentException(
            $"{fieldName} must be one of Neither, Email, Teams, Both, or left blank to use the system default.",
            fieldName);
    }

    public static async Task<IResult> GetUsers(
        IUserRepository repo,
        IAuth0ManagementService auth0Management,
        CancellationToken cancellationToken)
    {
        var users = await repo.GetAllUsersAsync();
        var ordered = users
            .Where(user => user.Id > 0)
            .OrderBy(user => user.DisplayName ?? user.Email)
            .ToList();

        var response = new List<AdminUserResponse>(ordered.Count);
        foreach (var user in ordered)
        {
            var roleNames = await GetAuth0RoleNamesForUserAsync(
                user,
                auth0Management,
                fallbackToLocalRole: true,
                cancellationToken);
            response.Add(user.ToAdminResponse(roleNames));
        }

        return Results.Ok(response);
    }

    public static async Task<IResult> GetAvailableAuth0Roles(
        IAuth0ManagementService auth0Management,
        CancellationToken cancellationToken)
    {
        try
        {
            var roles = await auth0Management.GetAllRolesAsync(cancellationToken);
            return Results.Ok(roles.OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase).ToList());
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(
                title: "Auth0 management is not configured",
                detail: exception.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (Auth0ManagementException exception)
        {
            return Results.Problem(
                title: "Failed to list Auth0 roles",
                detail: exception.Message,
                statusCode: exception.StatusCode is >= 400 and < 500
                    ? exception.StatusCode
                    : StatusCodes.Status502BadGateway);
        }
    }

    public static async Task<IResult> GetUserAuth0Roles(
        int id,
        IUserRepository repo,
        IAuth0ManagementService auth0Management,
        CancellationToken cancellationToken)
    {
        var user = await repo.GetByIdAsync(id);
        if (user is null)
        {
            return Results.NotFound($"User {id} was not found.");
        }

        if (string.IsNullOrWhiteSpace(user.Auth0Id))
        {
            return Results.Ok(new UserAuth0RolesResponse { Roles = [] });
        }

        try
        {
            var roles = await auth0Management.GetUserRolesAsync(user.Auth0Id!, cancellationToken);
            var dtos = roles
                .OrderBy(r => r.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return Results.Ok(new UserAuth0RolesResponse { Roles = dtos });
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(
                title: "Auth0 management is not configured",
                detail: exception.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
        catch (Auth0ManagementException exception)
        {
            return Results.Problem(
                title: "Failed to load Auth0 roles",
                detail: exception.Message,
                statusCode: exception.StatusCode is >= 400 and < 500
                    ? exception.StatusCode
                    : StatusCodes.Status502BadGateway);
        }
    }

    public static async Task<IResult> MutateUserAuth0Role(
        int id,
        UserRoleMutationRequest request,
        IUserRepository repo,
        IAuth0ManagementService auth0Management,
        IAuth0UserRoleSyncService roleSync,
        CancellationToken cancellationToken)
    {
        var action = request.Action?.Trim().ToLowerInvariant();
        if (action is not ("add" or "remove"))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["action"] = ["Action must be \"add\" or \"remove\"."]
            });
        }

        if (!Auth0Roles.TryNormalize(request.RoleName, out var canonicalName))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["roleName"] = ["Unknown role. Use Admin, Developer, Business Manager, User, or Guest."]
            });
        }

        var user = await repo.GetByIdAsync(id);
        if (user is null)
        {
            return Results.NotFound($"User {id} was not found.");
        }

        if (string.IsNullOrWhiteSpace(user.Auth0Id))
        {
            return Results.BadRequest(new { message = "This user has no Auth0 account; roles cannot be changed via Auth0." });
        }

        try
        {
            var allRoles = await auth0Management.GetAllRolesAsync(cancellationToken);
            var match = allRoles.FirstOrDefault(r =>
                r.Name.Equals(canonicalName, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                return Results.NotFound(new
                {
                    message = $"Role \"{canonicalName}\" is not defined in Auth0. Create it in Auth0 Dashboard → User Management → Roles."
                });
            }

            var current = await auth0Management.GetUserRolesAsync(user.Auth0Id!, cancellationToken);
            var hasRole = current.Any(r => string.Equals(r.Id, match.Id, StringComparison.Ordinal));

            if (action == "add")
            {
                if (hasRole)
                {
                    return Results.Conflict(new { message = "The user already has this role." });
                }

                await auth0Management.AssignRolesToUserAsync(user.Auth0Id!, [match.Id], cancellationToken);
            }
            else
            {
                if (!hasRole)
                {
                    return Results.Conflict(new { message = "The user does not have this role." });
                }

                await auth0Management.RemoveRolesFromUserAsync(user.Auth0Id!, [match.Id], cancellationToken);
            }

            var fresh = await auth0Management.GetUserRolesAsync(user.Auth0Id!, cancellationToken);
            var nameList = NormalizeAuth0RoleNames(fresh);
            user.Role = Auth0Roles.GetHighestRole(nameList);
            user.LastModifiedDate = DateTime.UtcNow;
            await repo.SaveChangesAsync();
            await roleSync.SyncRoleToAuth0Async(user, cancellationToken);

            return Results.Ok(user.ToAdminResponse(nameList));
        }
        catch (Auth0ManagementException exception)
        {
            return Results.Problem(
                title: "Auth0 role change failed",
                detail: exception.Message,
                statusCode: exception.StatusCode is >= 400 and < 600
                    ? exception.StatusCode
                    : StatusCodes.Status502BadGateway);
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(
                title: "Auth0 management is not configured",
                detail: exception.Message,
                statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    private static List<string> NormalizeAuth0RoleNames(IEnumerable<Auth0RoleDto> dtos)
    {
        var list = new List<string>();
        foreach (var dto in dtos)
        {
            if (Auth0Roles.TryNormalize(dto.Name, out var canonical))
            {
                if (!list.Contains(canonical, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(canonical);
                }
            }
            else if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                var raw = dto.Name.Trim();
                if (!list.Contains(raw, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(raw);
                }
            }
        }

        return list;
    }

    private static async Task<List<string>> GetAuth0RoleNamesForUserAsync(
        User user,
        IAuth0ManagementService auth0Management,
        bool fallbackToLocalRole,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(user.Auth0Id))
        {
            return new List<string> { user.Role };
        }

        try
        {
            var dtos = await auth0Management.GetUserRolesAsync(user.Auth0Id!, cancellationToken);
            return NormalizeAuth0RoleNames(dtos);
        }
        catch
        {
            return fallbackToLocalRole
                ? new List<string> { user.Role }
                : new List<string>();
        }
    }

    public static async Task<IResult> GetCurrentUser(
        IUserContextService userContext,
        IHttpContextAccessor httpContextAccessor)
    {
        var user = await userContext.GetCurrentUserAsync();
        var principal = httpContextAccessor.HttpContext?.User;
        var roles = principal is not null
            ? JwtRoleClaims.ResolveRoles(principal)
            : Array.Empty<string>();

        return Results.Ok(user.ToResponse(roles));
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

    public static async Task<IResult> GetUserDirectory(IUserRepository repo)
    {
        var users = await repo.GetAllUsersAsync();
        var directoryEntries = users
            .Where(user => user.Id > 0 && user.IsActive)
            .OrderBy(user => user.DisplayName ?? user.Email)
            .Select(user => user.ToDirectoryResponse())
            .ToList();

        return Results.Ok(directoryEntries);
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
        try
        {
            var user = await userContext.GetCurrentUserAsync();

            await userContext.UpdateProfileAsync(user, request);

            return Results.Ok(user.ToResponse());
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    public static async Task<IResult> UpdateUser(
        int id,
        AdminUpdateUserRequest request,
        IUserRepository repo,
        IAuth0ManagementService auth0Management,
        IAuth0UserRoleSyncService roleSync,
        CancellationToken cancellationToken)
    {
        var user = await repo.GetByIdAsync(id);
        if (user is null)
        {
            return Results.NotFound($"User {id} was not found.");
        }

        var role = user.Role;
        var roleExplicitlyChanged = false;
        if (!string.IsNullOrWhiteSpace(request.Role))
        {
            if (!TryParseRole(request.Role, out var parsed))
            {
                return Results.ValidationProblem(new Dictionary<string, string[]>
                {
                    ["role"] = ["Role must be one of: Admin, Developer, Business Manager, User, Guest."]
                });
            }

            role = parsed;
            roleExplicitlyChanged = !string.Equals(user.Role, parsed, StringComparison.Ordinal);
        }

        try
        {
            user.NickName = NormalizeOptionalValue(request.NickName);
            user.PhoneNumber = NormalizeOptionalValue(request.PhoneNumber);
            user.Department = NormalizeOptionalValue(request.Department);
            user.AssignmentNotificationChannel = ParseNotificationChannelOrNull(
                request.AssignmentNotificationChannel,
                nameof(request.AssignmentNotificationChannel));
            user.SlaRiskNotificationChannel = ParseNotificationChannelOrNull(
                request.SlaRiskNotificationChannel,
                nameof(request.SlaRiskNotificationChannel));
            user.Role = role;
            user.IsActive = request.IsActive;
            user.ExpiryDate = request.ExpiryDate;
            user.LastModifiedDate = DateTime.UtcNow;

            await repo.SaveChangesAsync();
            if (roleExplicitlyChanged)
            {
                await roleSync.SyncRoleToAuth0Async(user, cancellationToken);
            }

            var freshNames = await GetAuth0RoleNamesForUserAsync(
                user,
                auth0Management,
                fallbackToLocalRole: true,
                cancellationToken);
            return Results.Ok(user.ToAdminResponse(freshNames));
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    public static async Task<IResult> CreateUser(
        CreateUserRequest request,
        IUserRepository repo,
        IUserContextService userContext,
        IAuth0ManagementService auth0ManagementService,
        IHttpContextAccessor httpContextAccessor,
        CancellationToken cancellationToken)
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

        if (!TryParseRole(request.Role, out var role))
        {
            return Results.ValidationProblem(new Dictionary<string, string[]>
            {
                ["role"] = ["Role must be one of: Admin, Developer, Business Manager, User, Guest."]
            });
        }

        var normalizedEmail = request.Email.Trim();
        var existingUser = await repo.GetByEmailAsync(normalizedEmail);
        if (existingUser is not null)
        {
            return Results.Conflict(new { message = "A user with this email already exists." });
        }

        var isAdminCaller = httpContextAccessor.HttpContext?.User.IsInRole(Auth0Roles.Admin) == true;

        if (!isAdminCaller && role.Equals(Auth0Roles.Admin, StringComparison.Ordinal))
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

            try
            {
                var allRoles = await auth0ManagementService.GetAllRolesAsync(cancellationToken);
                var match = allRoles.FirstOrDefault(r =>
                    r.Name.Equals(role, StringComparison.OrdinalIgnoreCase));
                if (match is not null)
                {
                    await auth0ManagementService.AssignRolesToUserAsync(
                        createdAuth0UserId!,
                        [match.Id],
                        cancellationToken);
                }
            }
            catch
            {
                // Provisioning succeeded; RBAC role assignment can be completed in Admin UI.
            }

            var createdUser = await repo.GetByIdAsync(user.Id) ?? user;
            List<string> nameList;
            try
            {
                nameList = NormalizeAuth0RoleNames(
                    await auth0ManagementService.GetUserRolesAsync(createdAuth0UserId!, cancellationToken));
            }
            catch
            {
                nameList = new List<string> { role };
            }

            if (nameList.Count == 0)
            {
                nameList = new List<string> { role };
            }

            createdUser.Role = Auth0Roles.GetHighestRole(nameList);
            createdUser.LastModifiedDate = DateTime.UtcNow;
            await repo.SaveChangesAsync();

            return Results.Created($"/api/users/{createdUser.Id}", createdUser.ToAdminResponse(nameList));
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

    public static async Task<IResult> DeleteUser(
        int id,
        IUserRepository repo,
        IUserContextService userContext,
        IAuth0ManagementService auth0ManagementService,
        CortexDbContext dbContext)
    {
        var user = await repo.GetByIdAsync(id);
        if (user is null)
        {
            return Results.NotFound($"User {id} was not found.");
        }

        if (user.Id == 0)
        {
            return Results.BadRequest(new
            {
                message = "The fallback legacy user cannot be deleted."
            });
        }

        var currentUser = await userContext.GetCurrentUserAsync();
        if (user.Id == currentUser.Id)
        {
            return Results.BadRequest(new
            {
                message = "You cannot delete your own account."
            });
        }

        try
        {
            if (!string.IsNullOrWhiteSpace(user.Auth0Id))
            {
                await auth0ManagementService.DeleteUserAsync(user.Auth0Id);
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync();
            var legacyUserId = await EnsureLegacyUserAsync(dbContext);
            await ReassignDeletedUserReferencesAsync(
                dbContext,
                user.Id,
                legacyUserId,
                currentUser.Id,
                user.DisplayName ?? user.Email);

            dbContext.Users.Remove(user);
            await dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            return Results.NoContent();
        }
        catch (Auth0ManagementException exception)
        {
            return Results.Problem(
                title: "Failed to delete user from Auth0",
                detail: exception.Message,
                statusCode: exception.StatusCode switch
                {
                    400 => StatusCodes.Status400BadRequest,
                    401 or 403 => StatusCodes.Status502BadGateway,
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
    }

    private static async Task<int> EnsureLegacyUserAsync(CortexDbContext dbContext)
    {
        if (await dbContext.Users.AnyAsync(user => user.Id == 0))
        {
            return 0;
        }

        await EfSqlGuardrails.EnsureLegacyUserExistsAsync(dbContext.Database);

        return 0;
    }

    private static async Task ReassignDeletedUserReferencesAsync(
        CortexDbContext dbContext,
        int deletedUserId,
        int legacyUserId,
        int replacementJobRunAsUserId,
        string deletedUserLabel)
    {
        var utcNow = DateTime.UtcNow;
        var jobFailureMessage =
            $"Run-as user \"{deletedUserLabel}\" was deleted. Review this job before re-enabling it.";

        await dbContext.Tickets
            .Where(ticket => ticket.CreatedBy == deletedUserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(ticket => ticket.CreatedBy, legacyUserId));

        await dbContext.ArchivedTickets
            .Where(ticket => ticket.CreatedBy == deletedUserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(ticket => ticket.CreatedBy, legacyUserId));

        await dbContext.ArchivedTickets
            .Where(ticket => ticket.ArchivedBy == deletedUserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(ticket => ticket.ArchivedBy, legacyUserId));

        await dbContext.Comments
            .Where(comment => comment.CreatedBy == deletedUserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(comment => comment.CreatedBy, legacyUserId));

        await dbContext.ArchivedComments
            .Where(comment => comment.CreatedBy == deletedUserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(comment => comment.CreatedBy, legacyUserId));

        await dbContext.TicketAttachments
            .Where(attachment => attachment.UploadedBy == deletedUserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(attachment => attachment.UploadedBy, legacyUserId));

        await dbContext.ArchivedTicketAttachments
            .Where(attachment => attachment.UploadedBy == deletedUserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(attachment => attachment.UploadedBy, legacyUserId));

        await dbContext.TicketAuditEntries
            .Where(entry => entry.ChangedBy == deletedUserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(entry => entry.ChangedBy, legacyUserId));

        await dbContext.ScheduledJobs
            .Where(job => job.RunAsUserId == deletedUserId)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(job => job.RunAsUserId, replacementJobRunAsUserId)
                .SetProperty(job => job.IsEnabled, false)
                .SetProperty(job => job.NextRunDateUtc, (DateTime?)null)
                .SetProperty(job => job.LastModifiedDateUtc, utcNow)
                .SetProperty(job => job.LastRunStatus, "Failed")
                .SetProperty(job => job.LastRunMessage, jobFailureMessage));
    }
}
