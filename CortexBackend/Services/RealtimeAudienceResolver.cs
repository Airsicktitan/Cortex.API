using Cortex.API.Data;
using Cortex.API.Models;

namespace Cortex.API.Services;

public sealed class RealtimeAudienceResolver(IUserRepository userRepository) : IRealtimeAudienceResolver
{
    private static readonly TimeSpan UserCacheTtl = TimeSpan.FromSeconds(10);

    private readonly IUserRepository _userRepository = userRepository;
    private readonly object _cacheLock = new();
    private List<User>? _cachedVisibleUsers;
    private DateTime _cachedVisibleUsersUntilUtc;

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
        var users = await GetVisibleUsersAsync(utcNow, cancellationToken);

        var aliases = OwnerFieldResolution.BuildAliasLookup(users);

        var recipients = new HashSet<int>();
        foreach (var user in users)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (HasGlobalVisibility(user) ||
                CanSeeCreatedTicket(user, createdBy) ||
                CanSeeAssignedTicket(user, synitiOwner, businessOwner, aliases))
            {
                recipients.Add(user.Id);
            }
        }

        return recipients.ToArray();
    }

    private async Task<List<User>> GetVisibleUsersAsync(
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        lock (_cacheLock)
        {
            if (_cachedVisibleUsers is not null && _cachedVisibleUsersUntilUtc > utcNow)
            {
                return [.. _cachedVisibleUsers];
            }
        }

        cancellationToken.ThrowIfCancellationRequested();

        var users = (await _userRepository.GetAllUsersAsync())
            .Where(user =>
                user.IsActive &&
                (user.ExpiryDate is null || user.ExpiryDate > utcNow))
            .ToList();

        cancellationToken.ThrowIfCancellationRequested();

        lock (_cacheLock)
        {
            _cachedVisibleUsers = users;
            _cachedVisibleUsersUntilUtc = utcNow.Add(UserCacheTtl);
        }

        return [.. users];
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

    private static bool CanSeeAssignedTicket(
        User user,
        string? synitiOwner,
        string? businessOwner,
        IReadOnlyDictionary<string, User> aliases)
    {
        if (HasGlobalVisibility(user))
        {
            return false;
        }

        return OwnerFieldResolution.TokenMatchesUser(user, synitiOwner, aliases) ||
               OwnerFieldResolution.TokenMatchesUser(user, businessOwner, aliases);
    }
}
