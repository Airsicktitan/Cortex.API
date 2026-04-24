using Cortex.API.Models;

namespace Cortex.API.Services;

/// <summary>
/// Strict owner policies: Syniti owner must be an active Developer with directory eligibility;
/// business owner must be active, directory-eligible, and not a Developer or Guest.
/// </summary>
public static class OwnerRoleAssignmentRules
{
    public static bool IsValidSynitiOwnerAssignment(User user) =>
        user.IsActive
        && user.IsSynitiOwnerEligible
        && string.Equals(user.Role, Auth0Roles.Developer, StringComparison.OrdinalIgnoreCase);

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

        if (!string.Equals(user.Role, Auth0Roles.Developer, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Syniti Owner must be a Developer.");
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
