using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Models;

namespace Cortex.API.Services;

public class Auth0UserDirectorySyncService(
    IUserRepository userRepository,
    IAuth0ManagementService auth0Management) : IAuth0UserDirectorySyncService
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IAuth0ManagementService _auth0Management = auth0Management;

    public async Task<SyncUsersFromAuth0Response> SyncFromAuth0Async(CancellationToken cancellationToken = default)
    {
        var auth0Users = await _auth0Management.GetAllDirectoryUsersAsync(cancellationToken);
        var response = new SyncUsersFromAuth0Response();

        foreach (var remote in auth0Users)
        {
            if (string.IsNullOrWhiteSpace(remote.UserId))
            {
                continue;
            }

            var auth0Id = remote.UserId.Trim();
            var emailRaw = string.IsNullOrWhiteSpace(remote.Email) ? null : remote.Email.Trim();
            var normalizedEmail = emailRaw is null ? null : emailRaw.ToLowerInvariant();

            if (normalizedEmail is null)
            {
                response.SkippedNoEmail++;
                continue;
            }

            var existingByAuth0 = await _userRepository.GetByAuth0IdAsync(auth0Id);
            if (existingByAuth0 is not null)
            {
                var apply = await TryApplyIdentityUpdateAsync(
                    existingByAuth0,
                    normalizedEmail,
                    remote,
                    cancellationToken);
                switch (apply)
                {
                    case IdentityApplyResult.Updated:
                        response.Updated++;
                        break;
                    case IdentityApplyResult.Unchanged:
                        response.Unchanged++;
                        break;
                    case IdentityApplyResult.EmailConflict:
                        response.SkippedEmailConflict++;
                        break;
                }

                continue;
            }

            var existingByEmail = await _userRepository.GetByEmailAsync(normalizedEmail);
            if (existingByEmail is not null)
            {
                if (!string.IsNullOrWhiteSpace(existingByEmail.Auth0Id) &&
                    !existingByEmail.Auth0Id.Equals(auth0Id, StringComparison.Ordinal))
                {
                    response.SkippedEmailConflict++;
                    continue;
                }

                existingByEmail.Auth0Id = auth0Id;
                ApplyIdentityFieldsFromAuth0(existingByEmail, normalizedEmail, remote);
                existingByEmail.LastModifiedDate = DateTime.UtcNow;
                await _userRepository.SaveChangesAsync();
                response.LinkedByEmail++;
                continue;
            }

            var roleNames = await GetCanonicalRoleNamesAsync(auth0Id, cancellationToken);
            var displayName = PickDisplayName(remote, normalizedEmail);
            var resolvedRole = Auth0Roles.GetHighestRole(roleNames);
            var user = new User
            {
                Auth0Id = auth0Id,
                Email = normalizedEmail,
                DisplayName = displayName,
                NickName = ResolveNicknameForNewUser(remote, normalizedEmail),
                PhoneNumber = null,
                Department = UserDepartmentPolicy.ApplyDeveloperDepartmentDefault(null, resolvedRole),
                Role = resolvedRole,
                // Newly discovered Auth0 users land as inactive/pending. An admin must
                // explicitly approve them via the user-management endpoints before the
                // approval gate (see IAccessApprovalService) will let them in.
                IsActive = false,
                CreatedDate = DateTime.UtcNow,
                LastModifiedDate = DateTime.UtcNow,
            };

            await _userRepository.CreateUserAsync(user);
            await _userRepository.SaveChangesAsync();
            response.Created++;
        }

        response.TotalFromAuth0 = auth0Users.Count;
        return response;
    }

    private enum IdentityApplyResult
    {
        Updated,
        Unchanged,
        EmailConflict,
    }

    private async Task<IdentityApplyResult> TryApplyIdentityUpdateAsync(
        User local,
        string normalizedEmail,
        Auth0DirectoryUserDto remote,
        CancellationToken cancellationToken)
    {
        var changed = false;

        if (!string.Equals(local.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            var other = await _userRepository.GetByEmailAsync(normalizedEmail);
            if (other is not null && other.Id != local.Id)
            {
                return IdentityApplyResult.EmailConflict;
            }

            local.Email = normalizedEmail;
            changed = true;
        }

        // Sync must not approve access. We honor remote-side blocks (active → inactive)
        // as a hygiene signal, but we never silently flip a local inactive user active
        // based on Auth0 directory state alone — approval requires an explicit admin
        // action through the user-management endpoints.
        if (local.IsActive && remote.Blocked)
        {
            local.IsActive = false;
            changed = true;
        }

        var displayName = PickDisplayName(remote, normalizedEmail);
        if (!string.IsNullOrWhiteSpace(displayName) &&
            !string.Equals(local.DisplayName, displayName, StringComparison.Ordinal))
        {
            local.DisplayName = displayName;
            changed = true;
        }

        if (TryGetSyncedNicknameFromDirectory(remote, out var syncedNick) &&
            !string.Equals(local.NickName, syncedNick, StringComparison.Ordinal))
        {
            local.NickName = syncedNick;
            changed = true;
        }

        if (!changed)
        {
            return IdentityApplyResult.Unchanged;
        }

        local.LastModifiedDate = DateTime.UtcNow;
        await _userRepository.SaveChangesAsync();
        return IdentityApplyResult.Updated;
    }

    private static void ApplyIdentityFieldsFromAuth0(User local, string normalizedEmail, Auth0DirectoryUserDto remote)
    {
        local.Email = normalizedEmail;
        // See TryApplyIdentityUpdateAsync: honor a remote block but never grant access
        // via sync. An inactive local row stays inactive until an admin approves it.
        if (local.IsActive && remote.Blocked)
        {
            local.IsActive = false;
        }
        var displayName = PickDisplayName(remote, normalizedEmail);
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            local.DisplayName = displayName;
        }

        if (TryGetSyncedNicknameFromDirectory(remote, out var linkedNick))
        {
            local.NickName = linkedNick;
        }
    }

    private static string PickDisplayName(Auth0DirectoryUserDto remote, string normalizedEmail)
    {
        if (!string.IsNullOrWhiteSpace(remote.Name))
        {
            return remote.Name.Trim();
        }

        var at = normalizedEmail.IndexOf('@', StringComparison.Ordinal);
        return at > 0 ? normalizedEmail[..at] : normalizedEmail;
    }

    /// <summary>
    /// When Auth0 includes a root <c>nickname</c> JSON member (including explicit
    /// <c>null</c> or <c>""</c>), mirror it to Cortex. When the member is omitted
    /// (<see cref="Auth0NicknameField.IsSpecified"/> is <see langword="false"/> on the
    /// default field), do not change the local nickname.
    /// </summary>
    private static bool TryGetSyncedNicknameFromDirectory(Auth0DirectoryUserDto remote, out string? nickname)
    {
        if (!remote.Nickname.IsSpecified)
        {
            nickname = null;
            return false;
        }

        nickname = remote.Nickname.NormalizedValue;
        return true;
    }

    private static string? ResolveNicknameForNewUser(Auth0DirectoryUserDto remote, string normalizedEmail)
    {
        if (!remote.Nickname.IsSpecified)
        {
            return EmailLocalPart(normalizedEmail);
        }

        return remote.Nickname.NormalizedValue;
    }

    private static string EmailLocalPart(string normalizedEmail)
    {
        var at = normalizedEmail.IndexOf('@', StringComparison.Ordinal);
        return at > 0 ? normalizedEmail[..at] : normalizedEmail;
    }

    private async Task<List<string>> GetCanonicalRoleNamesAsync(string auth0UserId, CancellationToken cancellationToken)
    {
        try
        {
            var dtos = await _auth0Management.GetUserRolesAsync(auth0UserId, cancellationToken);
            return NormalizeAuth0RoleNames(dtos);
        }
        catch
        {
            return [];
        }
    }

    private static List<string> NormalizeAuth0RoleNames(IEnumerable<Auth0RoleDto> dtos)
    {
        var list = new List<string>();
        foreach (var dto in dtos)
        {
            if (Auth0Roles.TryNormalize(dto.Name, out var canonical))
            {
                if (!list.Contains(canonical, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(canonical);
                }
            }
            else if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                var raw = dto.Name.Trim();
                if (!list.Contains(raw, StringComparer.OrdinalIgnoreCase))
                {
                    list.Add(raw);
                }
            }
        }

        return list;
    }
}
