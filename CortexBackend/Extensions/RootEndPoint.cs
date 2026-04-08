namespace Cortex.API.Extensions;

using Cortex.API.Models;
using Cortex.API.Database;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Defines all ticket-related API endpoints for CORTEX.
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

public static class RootEndpoints
{
    public static void MapRootEndpoint(this WebApplication app)
    {
        // Root endpoint
        app.MapGet("/", () => "🧠 CORTEX Online - Central Operations & Routing Technology EXpert")
            .WithName("Root")
            .WithTags("Health");
    }
}
