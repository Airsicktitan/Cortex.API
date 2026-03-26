namespace Cortex.API.Extensions;

using Cortex.API.Models;
using Cortex.API.Database;
using Cortex.API.Handlers;

using Microsoft.EntityFrameworkCore;
using Cortex.API.DTO;

/// <summary>
/// Defines all user-related API endpoints for CORTEX.
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
            .RequireAuthorization()
            .WithTags("Users");

        users.MapGet("/", UserHandlers.GetUsers)
            .WithName("GetUsers")
            .Produces<List<UserResponse>>(StatusCodes.Status200OK);

        users.MapGet("/me", UserHandlers.GetCurrentUser)
            .WithName("GetCurrentUser");
        
        users.MapPut("/profile", UserHandlers.UpdateUserProfile)
            .WithName("UpdateUserProfile")
            .Accepts<UpdateUserProfileRequest>("application/json")
            .Produces<UserResponse>(StatusCodes.Status200OK);
    }
}
