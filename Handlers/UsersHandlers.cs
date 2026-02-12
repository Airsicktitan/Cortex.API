namespace Cortex.API.Handlers;

using Cortex.API.Models;
using Cortex.API.DTOs;
using Cortex.API.Data;

/// <summary>
/// Defines all user-related API handlers for CORTEX.
/// Implements RESTful CRUD operations with database persistence via Entity Framework Core.
/// </summary>
public static class UserHandlers
{
    public static async Task<IResult> GetUsers(IUserRepository repo)
    {
        var users = await repo.GetAllUsersAsync();
        
        if (users.Count() == 0)
            return Results.NotFound("No users found.");
        
        var response = users.Select(user => new UserResponse
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email,
            Department = user.Department,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            CreatedDate = user.CreatedDate,
            LastLoginDate = user.LastLoginDate,
            LastModifiedDate = user.LastModifiedDate
        });

        return Results.Ok(response.ToList());
    }

    public static async Task<IResult> GetCurrentUser(
        HttpContext http, 
        IUserRepository repo, 
        IConfiguration config)
    {
        // Extract Auth0 user ID from JWT claims
        var auth0Id = http.User.FindFirst("sub")?.Value;
        
        if (auth0Id == null)
            return Results.Unauthorized();

        string email = "";
        string name = "Unknown";
        
        try
        {
            // Extract bearer token from request headers
            var authHeader = http.Request.Headers.Authorization.ToString();
            var token = authHeader.Replace("Bearer ", "");

            // Call Auth0 UserInfo endpoint to get user profile
            using var httpClient = new System.Net.Http.HttpClient();
            httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");

            var userInfoUrl = $"https://{config["Auth0:Domain"]}/userinfo";
            var response = await httpClient.GetAsync(userInfoUrl);

            if (response.IsSuccessStatusCode)
            {
                var userInfo = await response.Content.ReadFromJsonAsync<Dictionary<string, System.Text.Json.JsonElement>>();
                
                if (userInfo != null)
                {
                    if (userInfo.TryGetValue("email", out var emailElement))
                        email = emailElement.GetString() ?? "";
                    
                    if (userInfo.TryGetValue("name", out var nameElement))
                        name = nameElement.GetString() ?? "Unknown";
                }
            }
        }
        catch
        {
            // Log error in production, but continue with default values
            // TODO: Add proper logging
        }

        // Find or create user in database
        var user = await repo.GetByAuth0IdAsync(auth0Id);

        if (user == null)
        {
            // Create new user
            user = new User
            {
                Auth0Id = auth0Id,
                DisplayName = name,
                Email = email,
                CreatedDate = DateTime.UtcNow,
                LastLoginDate = DateTime.UtcNow
            };
            await repo.CreateUserAsync(user);
        }
        else
        {
            // Update existing user with latest info from Auth0
            bool needsUpdate = false;
            
            if (user.DisplayName != name)
            {
                user.DisplayName = name;
                needsUpdate = true;
            }
            
            if (user.Email != email)
            {
                user.Email = email;
                needsUpdate = true;
            }
            
            if (needsUpdate)
            {
                user.LastModifiedDate = DateTime.UtcNow;
            }
            
            // Always update last login
            user.LastLoginDate = DateTime.UtcNow;
        }

        await repo.SaveChangesAsync();

        return Results.Ok(new UserResponse
        {
            Id = user.Id,
            DisplayName = user.DisplayName,
            Email = user.Email,
            Department = user.Department,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            CreatedDate = user.CreatedDate,
            LastLoginDate = user.LastLoginDate,
            LastModifiedDate = user.LastModifiedDate
        });
    }
}