namespace Cortex.API.Extensions;

using Cortex.API.Authorization;
using Cortex.API.Handlers;
using Cortex.API.DTO;

/// <summary>
/// User API: management routes use Auth0-aligned policies; profile routes are authenticated.
/// </summary>
public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        var users = app.MapGroup("/api/users")
            .RequireAuthorization()
            .WithTags("Users");

        users.MapGet("/", UserHandlers.GetUsers)
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .WithName("GetUsers")
            .Produces<List<AdminUserResponse>>(StatusCodes.Status200OK);

        users.MapGet("/available-roles", UserHandlers.GetAvailableAuth0Roles)
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .WithName("GetAvailableAuth0Roles")
            .Produces<List<Auth0RoleDto>>(StatusCodes.Status200OK);

        users.MapPost("/sync-from-auth0", UserHandlers.SyncUsersFromAuth0)
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .WithName("SyncUsersFromAuth0")
            .Produces<SyncUsersFromAuth0Response>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status500InternalServerError)
            .ProducesProblem(StatusCodes.Status502BadGateway);

        users.MapGet("/{id:int}/roles", UserHandlers.GetUserAuth0Roles)
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .WithName("GetUserAuth0Roles")
            .Produces<UserAuth0RolesResponse>(StatusCodes.Status200OK);

        users.MapPost("/{id:int}/roles/mutation", UserHandlers.MutateUserAuth0Role)
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .WithName("MutateUserAuth0Role")
            .Accepts<UserRoleMutationRequest>("application/json")
            .Produces<AdminUserResponse>(StatusCodes.Status200OK);

        users.MapGet("/online", UserHandlers.GetOnlineUsers)
            .RequireAuthorization(CortexAuthorizationExtensions.BusinessAccess)
            .WithName("GetOnlineUsers")
            .Produces<List<OnlineUserResponse>>(StatusCodes.Status200OK);

        users.MapGet("/directory", UserHandlers.GetUserDirectory)
            .RequireAuthorization(CortexAuthorizationExtensions.StandardWriteAccess)
            .WithName("GetUserDirectory")
            .Produces<List<UserDirectoryEntryResponse>>(StatusCodes.Status200OK);

        users.MapPost("/", UserHandlers.CreateUser)
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .WithName("CreateUser")
            .Accepts<CreateUserRequest>("application/json")
            .Produces<AdminUserResponse>(StatusCodes.Status201Created);

        users.MapPut("/{id:int}", UserHandlers.UpdateUser)
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .WithName("UpdateUser")
            .Accepts<AdminUpdateUserRequest>("application/json")
            .Produces<AdminUserResponse>(StatusCodes.Status200OK);

        users.MapDelete("/{id:int}", UserHandlers.DeleteUser)
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .WithName("DeleteUser")
            .Produces(StatusCodes.Status204NoContent)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        users.MapGet("/me", UserHandlers.GetCurrentUser)
            .WithName("GetCurrentUser")
            .Produces<UserResponse>(StatusCodes.Status200OK);

        users.MapPost("/me/presence", UserHandlers.UpdateCurrentUserPresence)
            .WithName("UpdateCurrentUserPresence")
            .Produces(StatusCodes.Status204NoContent);

        users.MapPut("/profile", UserHandlers.UpdateUserProfile)
            .WithName("UpdateUserProfile")
            .Accepts<UpdateUserProfileRequest>("application/json")
            .Produces<UserResponse>(StatusCodes.Status200OK);
    }
}
