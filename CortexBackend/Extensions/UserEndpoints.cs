namespace Cortex.API.Extensions;

using Cortex.API.Models;
using Cortex.API.Database;
using Cortex.API.Handlers;

using Microsoft.EntityFrameworkCore;
using Cortex.API.DTO;

/// <summary>
/// Defines all user-related API endpoints for CORTEX.
/// Implements RESTful CRUD operations with database persistence via Entity Framework Core.

/// - JWT authentication
/// - Role-based authorization (admin vs regular user)

/// Known Limitations:
/// - POST endpoint uses client-side ID generation (see inline TODO)
/// - Authentication/authorization not yet implemented (see inline TODOs)
/// - Delete endpoint commented out pending auth implementation
/// 
/// Future Enhancements:

/// - Audit logging for all operations
/// - Input validation middleware
/// </summary>

public static class UserEndpoints
{
    public static void MapUserEndpoints(this WebApplication app)
    {
        // User endpoints can be added here similarly
        var users = app.MapGroup("/api/users")
            .RequireAuthorization()
            .WithTags("Users");

        users.MapGet("/", UserHandlers.GetUsers)
            .RequireAuthorization("UsersAdminRead")
            .WithName("GetUsers")
            .Produces<List<AdminUserResponse>>(StatusCodes.Status200OK);

        users.MapGet("/online", UserHandlers.GetOnlineUsers)
            .RequireAuthorization("UsersAdminRead")
            .WithName("GetOnlineUsers")
            .Produces<List<OnlineUserResponse>>(StatusCodes.Status200OK);

        users.MapPost("/", UserHandlers.CreateUser)
            .RequireAuthorization("UsersCreate")
            .WithName("CreateUser")
            .Accepts<CreateUserRequest>("application/json")
            .Produces<AdminUserResponse>(StatusCodes.Status201Created);

        users.MapPut("/{id:int}", UserHandlers.UpdateUser)
            .RequireAuthorization("UsersAdminUpdate")
            .WithName("UpdateUser")
            .Accepts<AdminUpdateUserRequest>("application/json")
            .Produces<AdminUserResponse>(StatusCodes.Status200OK);

        users.MapPut("/{id:int}/access", UserHandlers.UpdateUserAccess)
            .RequireAuthorization("UsersAccessManage")
            .WithName("UpdateUserAccess")
            .Accepts<UpdateUserAccessRequest>("application/json")
            .Produces<UserAccessUpdateResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        users.MapDelete("/{id:int}", UserHandlers.DeleteUser)
            .RequireAuthorization("UsersAdminDelete")
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
