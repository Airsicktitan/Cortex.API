using Cortex.API.Data;
using Cortex.API.Models;

namespace Cortex.API.Services;

public static class TicketOwnerAssignmentValidation
{
    public sealed record NormalizedOwners(string? SynitiOwner, string? BusinessOwner);

    public static async Task ValidateAsync(
        IUserRepository userRepository,
        string? synitiOwner,
        string? businessOwner)
    {
        await NormalizeAndValidateAsync(userRepository, synitiOwner, businessOwner);
    }

    public static async Task<NormalizedOwners> NormalizeAndValidateAsync(
        IUserRepository userRepository,
        string? synitiOwner,
        string? businessOwner)
    {
        var users = (await userRepository.GetAllUsersAsync()).ToList();
        var aliases = OwnerFieldResolution.BuildAliasLookup(users);

        return new NormalizedOwners(
            NormalizeSlot(synitiOwner, synitiSlot: true, aliases),
            NormalizeSlot(businessOwner, synitiSlot: false, aliases));
    }

    private static string? NormalizeSlot(
        string? raw,
        bool synitiSlot,
        IReadOnlyDictionary<string, User> aliases)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var resolved = OwnerFieldResolution.ResolveUser(raw, aliases);
        if (resolved is null)
        {
            throw new ArgumentException(
                synitiSlot
                    ? "Syniti owner must reference a user from the directory."
                    : "Business owner must reference a user from the directory.");
        }

        if (synitiSlot)
        {
            OwnerRoleAssignmentRules.EnsureSynitiOwnerAssignment(resolved);
        }
        else
        {
            OwnerRoleAssignmentRules.EnsureBusinessOwnerAssignment(resolved);
        }

        return OwnerFieldResolution.ToCanonicalOwnerKey(resolved);
    }
}
