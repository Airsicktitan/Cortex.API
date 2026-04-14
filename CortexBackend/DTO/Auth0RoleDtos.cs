namespace Cortex.API.DTO;

/// <summary>Role as returned by Auth0 Management API (GET /api/v2/roles, GET .../users/.../roles).</summary>
public class Auth0RoleDto
{
    public required string Id { get; set; }
    public required string Name { get; set; }
}

public class UserAuth0RolesResponse
{
    public required List<Auth0RoleDto> Roles { get; set; }
}

/// <summary>Add or remove a single role by canonical name (matches Auth0 role name).</summary>
public class UserRoleMutationRequest
{
    /// <summary>"add" or "remove".</summary>
    public required string Action { get; set; }

    /// <summary>Role name, e.g. Admin, Developer, Business Manager.</summary>
    public required string RoleName { get; set; }
}
