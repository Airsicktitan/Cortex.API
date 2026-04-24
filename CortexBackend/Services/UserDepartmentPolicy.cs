using Cortex.API.Models;

namespace Cortex.API.Services;

/// <summary>
/// Default department for Developer users. Empty department on create/import/role elevation
/// is filled with <see cref="DefaultDeveloperDepartment"/>; non-empty values are never overwritten here.
/// </summary>
public static class UserDepartmentPolicy
{
    public const string DefaultDeveloperDepartment = "Syniti";

    /// <summary>
    /// If <paramref name="role"/> is Developer and <paramref name="department"/> is null/whitespace,
    /// returns <see cref="DefaultDeveloperDepartment"/>; otherwise returns <paramref name="department"/>
    /// (trimmed non-empty) unchanged.
    /// </summary>
    public static string? ApplyDeveloperDepartmentDefault(string? department, string role)
    {
        if (!string.Equals(role, Auth0Roles.Developer, StringComparison.OrdinalIgnoreCase))
        {
            return string.IsNullOrWhiteSpace(department) ? null : department.Trim();
        }

        if (string.IsNullOrWhiteSpace(department))
        {
            return DefaultDeveloperDepartment;
        }

        return department.Trim();
    }
}
