using System.Security.Claims;
using Cortex.API.Models;

namespace Cortex.API.Services;

public interface IUserContextService
{
    public Task<User> GetCurrentUserAsync(ClaimsPrincipal principal);
}