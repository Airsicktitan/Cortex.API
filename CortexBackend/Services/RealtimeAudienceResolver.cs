using Cortex.API.Data;
using Cortex.API.Models;

namespace Cortex.API.Services;

public sealed class RealtimeAudienceResolver(IUserRepository userRepository) : IRealtimeAudienceResolver
{
    private readonly IUserRepository _userRepository = userRepository;

    public Task<int[]> GetAudienceUserIdsAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default)
    {
        return GetAudienceUserIdsAsync(
            ticket.CreatedBy,
            ticket.SynitiOwner,
            ticket.BusinessOwner,
            cancellationToken);
    }

    public Task<int[]> GetAudienceUserIdsAsync(
        ArchivedTicket ticket,
        CancellationToken cancellationToken = default)
    {
        return GetAudienceUserIdsAsync(
            ticket.CreatedBy,
            ticket.SynitiOwner,
            ticket.BusinessOwner,
            cancellationToken);
    }

    public async Task<int[]> GetAudienceUserIdsAsync(
        int createdBy,
        string? synitiOwner,
        string? businessOwner,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var utcNow = DateTime.UtcNow;
        var users = (await _userRepository.GetAllUsersAsync())
            .Where(user =>
                user.IsActive &&
                (user.ExpiryDate is null || user.ExpiryDate > utcNow))
            .ToList();

        var recipients = new HashSet<int>();
        foreach (var user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (HasGlobalVisibility(user) ||
                CanSeeCreatedTicket(user, createdBy) ||
                CanSeeAssignedTicket(user, synitiOwner, businessOwner))
            {
                recipients.Add(user.Id);
            }
        }

        return recipients.ToArray();
    }

    private static bool HasGlobalVisibility(User user)
    {
        return string.Equals(user.Role, Auth0Roles.Admin, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(user.Role, Auth0Roles.Developer, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(user.Role, Auth0Roles.BusinessManager, StringComparison.OrdinalIgnoreCase);
    }

    private static bool CanSeeCreatedTicket(User user, int createdBy)
    {
        return !string.Equals(user.Role, Auth0Roles.Guest, StringComparison.OrdinalIgnoreCase) &&
               user.Id == createdBy;
    }

    private static bool CanSeeAssignedTicket(User user, string? synitiOwner, string? businessOwner)
    {
        if (HasGlobalVisibility(user))
        {
            return false;
        }

        return MatchesIdentity(user, synitiOwner) || MatchesIdentity(user, businessOwner);
    }

    private static bool MatchesIdentity(User user, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return string.Equals(value.Trim(), user.DisplayName?.Trim(), StringComparison.OrdinalIgnoreCase) ||
               string.Equals(value.Trim(), user.Email?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}
