using System.Security.Claims;
using Cortex.API.DTO;
using Cortex.API.Models;

namespace Cortex.API.Services;

public interface IUserContextService
{
    public Task<User> GetCurrentUserAsync();
    public Task<User> GetCurrentUserAsync(
        ClaimsPrincipal? principal,
        CancellationToken cancellationToken = default);
    public Task<User> UpdateProfileAsync(User user, UpdateUserProfileRequest request);
}