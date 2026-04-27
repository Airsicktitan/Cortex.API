using Cortex.API.Models;

namespace Cortex.API.Services;

/// <summary>
/// Strict owner policies: Syniti owner must be active, in the Syniti department, and directory-eligible;
/// business owner must be active, directory-eligible, and not a Developer or Guest.
/// </summary>
public static class OwnerRoleAssignmentRules
{
    private static bool IsSynitiDepartment(string? department) =>
        !string.IsNullOrWhiteSpace(department)
        && string.Equals(
            department.Trim(),
            UserDepartmentPolicy.DefaultDeveloperDepartment,
            StringComparison.OrdinalIgnoreCase);

    public static bool IsValidSynitiOwnerAssignment(User user) =>
        user.IsActive
        && user.IsSynitiOwnerEligible
        && IsSynitiDepartment(user.Department);

    public static bool IsValidBusinessOwnerAssignment(User user) =>
        user.IsActive
        && user.IsBusinessOwnerEligible
        && !string.Equals(user.Role, Auth0Roles.Developer, StringComparison.OrdinalIgnoreCase)
        && !string.Equals(user.Role, Auth0Roles.Guest, StringComparison.OrdinalIgnoreCase);

    public static void EnsureSynitiOwnerAssignment(User user)
    {
        if (!user.IsActive)
        {
            throw new ArgumentException(
                "Syniti owner must reference an active user from the directory.");
        }

        if (!user.IsSynitiOwnerEligible)
        {
            throw new ArgumentException(
                "The selected user is not eligible to be assigned as Syniti owner.");
        }

        if (!IsSynitiDepartment(user.Department))
        {
            throw new ArgumentException(
                $"Syniti owner must belong to department '{UserDepartmentPolicy.DefaultDeveloperDepartment}'.");
        }
    }

    public static void EnsureBusinessOwnerAssignment(User user)
    {
        if (!user.IsActive)
        {
            throw new ArgumentException(
                "Business owner must reference an active user from the directory.");
        }

        if (!user.IsBusinessOwnerEligible)
        {
            throw new ArgumentException(
                "The selected user is not eligible to be assigned as business owner.");
        }

        if (string.Equals(user.Role, Auth0Roles.Developer, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Business Owner cannot be a Developer.");
        }

        if (string.Equals(user.Role, Auth0Roles.Guest, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Business Owner cannot be a Guest.");
        }
    }
}
