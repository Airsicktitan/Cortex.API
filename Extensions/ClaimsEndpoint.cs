namespace Cortex.API.Extensions;

public static class ClaimsEndpoint
{
    public static void MapClaimEndpoint(this WebApplication app)
    {
        var claims = app.MapGroup("/api/claims")
            .RequireAuthorization();

        claims.MapGet("/", async (HttpContext httpContext) =>
        {
            var userClaims = httpContext.User.Claims
                .Select(c => new 
                    { 
                        c.Type, c.Value 
                    })
                .ToList();
            
            return Results.Ok(userClaims);
        }
        ).WithName("GetUserClaims")
         .Produces<List<object>>(StatusCodes.Status200OK);

        claims.MapGet("/admin", () =>
        {
            return Results.Ok("Admin endpoint - access granted"); 
        })
        .RequireAuthorization("AdminSystem");

        claims.MapGet("/user", () =>
        {
            return Results.Ok("User endpoint - access granted"); 
        })
        .RequireAuthorization("UserRole");

    }
}
