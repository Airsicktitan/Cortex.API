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
            var user = new User
            {
                Auth0Id = auth0Id,
                Email = normalizedEmail,
                DisplayName = displayName,
                NickName = NormalizeOptional(remote.Nickname),
                PhoneNumber = null,
                Department = null,
                Role = Auth0Roles.GetHighestRole(roleNames),
                IsActive = !remote.Blocked,
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

        var desiredActive = !remote.Blocked;
        if (local.IsActive != desiredActive)
        {
            local.IsActive = desiredActive;
            changed = true;
        }

        var displayName = PickDisplayName(remote, normalizedEmail);
        if (!string.IsNullOrWhiteSpace(displayName) &&
            !string.Equals(local.DisplayName, displayName, StringComparison.Ordinal))
        {
            local.DisplayName = displayName;
            changed = true;
        }

        var nick = NormalizeOptional(remote.Nickname);
        if (nick is not null &&
            !string.Equals(local.NickName, nick, StringComparison.Ordinal))
        {
            local.NickName = nick;
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
        local.IsActive = !remote.Blocked;
        var displayName = PickDisplayName(remote, normalizedEmail);
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            local.DisplayName = displayName;
        }

        var nick = NormalizeOptional(remote.Nickname);
        if (nick is not null)
        {
            local.NickName = nick;
        }
    }

    private static string PickDisplayName(Auth0DirectoryUserDto remote, string normalizedEmail)
    {
        if (!string.IsNullOrWhiteSpace(remote.Name))
        {
            return remote.Name.Trim();
        }

        if (!string.IsNullOrWhiteSpace(remote.Nickname))
        {
            return remote.Nickname.Trim();
        }

        var at = normalizedEmail.IndexOf('@', StringComparison.Ordinal);
        return at > 0 ? normalizedEmail[..at] : normalizedEmail;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
