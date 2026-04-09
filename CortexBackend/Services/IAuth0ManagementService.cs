using Cortex.API.DTO;

namespace Cortex.API.Services;

public interface IAuth0ManagementService
{
    Task<string> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(string auth0UserId, CancellationToken cancellationToken = default);
}
