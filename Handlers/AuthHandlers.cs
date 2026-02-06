namespace Cortex.API.Handlers;

using Cortex.API.Models;
using Cortex.API.Database;

using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Text;

public static class AuthHandlers
{
    public static async Task<IResult> Login(LoginRequest request, CortexDbContext db, IConfiguration config)
    {
        var key = config["Jwt:Key"];
        var issuer = config["Jwt:Issuer"];
        var audience = config["Jwt:Audience"];

        if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(issuer) || string.IsNullOrEmpty(audience))
        {
            return Results.BadRequest("JWT configuration is missing.");
        }

        // This is a placeholder implementation
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Username == request.Username && u.Password == request.Password);

        if (user == null)
        {
            return Results.Unauthorized();
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
        };

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        
        var expiration = DateTime.UtcNow.AddHours(1);

        var token = new JwtSecurityToken
        (
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiration,
            signingCredentials: credentials
        );

        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenString = tokenHandler.WriteToken(token);

        var response = new LoginResponse
        {
            Token = tokenString,
            Expiration = expiration
        };

        return Results.Ok(response);
    }

    public static async Task<IResult> Register(LoginRequest request, CortexDbContext db)
    {
        // Implement registration logic here (create user, hash password, etc.)
        // This is a placeholder implementation
        var userExists = await db.Users.AnyAsync(u => u.Username == request.Username);
        if (userExists)
        {
            return Results.BadRequest("Username already exists.");
        }

        var newUser = new User
        {
            Email = request.Username + "@example.com", // Placeholder email
            Username = request.Username,
            Password = request.Password // In real implementation, hash the password
        };

        db.Users.Add(newUser);
        await db.SaveChangesAsync();

        var token = "generated-jwt-token"; // Replace with actual token generation logic
        var expiration = DateTime.UtcNow.AddHours(1);

        var response = new LoginResponse
        {
            Token = token,
            Expiration = expiration
        };

        return Results.Created("/api/auth/login", response);
    }
}
