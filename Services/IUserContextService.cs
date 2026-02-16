using System.Security.Claims;
using Cortex.API.DTOs;
using Cortex.API.Models;

namespace Cortex.API.Services;

public interface IUserContextService
{
    public Task<User> GetCurrentUserAsync(ClaimsPrincipal principal);
    public Task<User> UpdateProfileAsync(User user, UpdateUserProfileRequest request);
}