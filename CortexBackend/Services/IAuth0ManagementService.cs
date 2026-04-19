using Cortex.API.DTO;

namespace Cortex.API.Services;

public interface IAuth0ManagementService
{
    Task<string> CreateUserAsync(CreateUserRequest request, CancellationToken cancellationToken = default);
    Task DeleteUserAsync(string auth0UserId, CancellationToken cancellationToken = default);

    /// <summary>GET /api/v2/roles — all roles defined in the Auth0 tenant (for admin UI).</summary>
    Task<IReadOnlyList<Auth0RoleDto>> GetAllRolesAsync(CancellationToken cancellationToken = default);

    /// <summary>GET /api/v2/users/{id}/roles — roles currently assigned to the user.</summary>
    Task<IReadOnlyList<Auth0RoleDto>> GetUserRolesAsync(string auth0UserId, CancellationToken cancellationToken = default);

    /// <summary>POST /api/v2/users/{id}/roles — assign roles by Auth0 role id.</summary>
    Task AssignRolesToUserAsync(string auth0UserId, IReadOnlyList<string> roleIds, CancellationToken cancellationToken = default);

    /// <summary>DELETE /api/v2/users/{id}/roles — remove roles by Auth0 role id.</summary>
    Task RemoveRolesFromUserAsync(string auth0UserId, IReadOnlyList<string> roleIds, CancellationToken cancellationToken = default);

    /// <summary>GET /api/v2/users (paginated) — all users in the Auth0 tenant for directory sync.</summary>
    Task<IReadOnlyList<Auth0DirectoryUserDto>> GetAllDirectoryUsersAsync(CancellationToken cancellationToken = default);
}
