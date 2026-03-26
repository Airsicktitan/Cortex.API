using Cortex.API.DTO;
using Cortex.API.Models;

namespace Cortex.API.Services;

public interface IUserContextService
{
    public Task<User> GetCurrentUserAsync();
    public Task<User> UpdateProfileAsync(User user, UpdateUserProfileRequest request);
}