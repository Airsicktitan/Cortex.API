namespace Cortex.API.Extensions;

using Cortex.API.Models;
using Cortex.API.Database;
using Cortex.API.Handlers;

using Microsoft.EntityFrameworkCore;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var auth = app.MapGroup("/api/auth")
            .WithTags("Authentication");

        auth.MapPost("/login", AuthHandlers.Login)
            .WithName("Login")
            .Produces<LoginResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);

        auth.MapPost("/register", AuthHandlers.Register)
            .WithName("Register")
            .Produces<LoginResponse>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest);
    }
}