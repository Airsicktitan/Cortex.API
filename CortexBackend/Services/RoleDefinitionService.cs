using Cortex.API.Data.Repositories;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public class RoleDefinitionService(
    IRoleDefinitionRepository repository,
    CortexDbContext context,
    IAuth0ManagementService auth0Management) : IRoleDefinitionService
{
    private readonly IRoleDefinitionRepository _repository = repository;
    private readonly CortexDbContext _context = context;
    private readonly IAuth0ManagementService _auth0Management = auth0Management;
    private static readonly string[] PermissionCatalog =
    [
        "View Tickets",
        "Edit Tickets",
        "Assign Tickets",
        "Manage Routing",
        "Admin Access"
    ];

    public IReadOnlyCollection<string> AllowedPermissions => PermissionCatalog;

    public async Task<IReadOnlyList<RoleDefinition>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<RoleDefinition> CreateAsync(RoleDefinition definition)
    {
        var normalized = Normalize(definition);
        await ValidateAsync(normalized, null);
        await _repository.AddAsync(normalized);
        await _repository.SaveChangesAsync();
        return normalized;
    }

    public async Task<RoleDefinition> UpdateAsync(int id, RoleDefinition definition)
    {
        var existing = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Role definition was not found.");

        var normalized = Normalize(definition);
        await ValidateAsync(normalized, id);

        existing.Name = normalized.Name;
        existing.NameNormalized = normalized.NameNormalized;
        existing.Description = normalized.Description;
        existing.Permissions = normalized.Permissions;
        existing.IsEnabled = normalized.IsEnabled;
        existing.LastModifiedDateUtc = DateTime.UtcNow;

        await _repository.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        var existing = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Role definition was not found.");

        var assignedUsers = await _context.Users
            .Where(user => user.Role.ToLower() == existing.Name.ToLower())
            .Select(user => user.DisplayName ?? user.Email)
            .OrderBy(name => name)
            .ToListAsync();

        if (assignedUsers.Count > 0)
        {
            var exampleUsers = string.Join(", ", assignedUsers.Take(3));
            throw new InvalidOperationException(
                $"Role \"{existing.Name}\" is assigned to {assignedUsers.Count} user(s) (for example: {exampleUsers}). Reassign those users before deleting this role.");
        }

        _repository.Delete(existing);
        await _repository.SaveChangesAsync();
    }

    public async Task<SyncRoleDefinitionsFromAuth0Response> SyncFromAuth0Async(
        CancellationToken cancellationToken = default)
    {
        var auth0Roles = await _auth0Management.GetAllRolesAsync(cancellationToken);
        var existing = await _repository.GetAllAsync();
        var existingNames = new HashSet<string>(
            existing.Select(role => role.Name),
            StringComparer.OrdinalIgnoreCase);

        var created = 0;
        var skippedExisting = 0;
        var totalFromAuth0 = 0;

        foreach (var auth0 in auth0Roles.OrderBy(role => role.Name, StringComparer.OrdinalIgnoreCase))
        {
            var trimmed = auth0.Name.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                continue;
            }

            totalFromAuth0++;

            if (existingNames.Contains(trimmed))
            {
                skippedExisting++;
                continue;
            }

            var draft = new RoleDefinition
            {
                Name = trimmed,
                Description = null,
                Permissions = DefaultPermissionsForAuth0RoleName(trimmed),
                IsEnabled = true,
                CreatedDateUtc = DateTime.UtcNow,
            };

            var normalized = Normalize(draft);
            await _repository.AddAsync(normalized);
            existingNames.Add(trimmed);
            created++;
        }

        if (created > 0)
        {
            await _repository.SaveChangesAsync();
        }

        return new SyncRoleDefinitionsFromAuth0Response
        {
            Created = created,
            SkippedExisting = skippedExisting,
            TotalFromAuth0 = totalFromAuth0,
        };
    }

    /// <summary>Default Cortex permissions when bootstrapping from Auth0 (subset of <see cref="PermissionCatalog"/>; may be empty).</summary>
    private static List<string> DefaultPermissionsForAuth0RoleName(string name)
    {
        if (name.Equals(Auth0Roles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return PermissionCatalog.ToList();
        }

        if (name.Equals(Auth0Roles.Developer, StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                "View Tickets",
                "Edit Tickets",
                "Assign Tickets",
                "Manage Routing",
            ];
        }

        if (name.Equals(Auth0Roles.BusinessManager, StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                "View Tickets",
                "Edit Tickets",
                "Assign Tickets",
            ];
        }

        if (name.Equals(Auth0Roles.User, StringComparison.OrdinalIgnoreCase))
        {
            return ["View Tickets", "Edit Tickets"];
        }

        if (name.Equals(Auth0Roles.Guest, StringComparison.OrdinalIgnoreCase))
        {
            return ["View Tickets"];
        }

        // Custom Auth0 roles: no Cortex permissions until an admin configures them.
        return [];
    }

    private async Task ValidateAsync(RoleDefinition definition, int? existingId)
    {
        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            throw new ArgumentException("Role name is required.");
        }

        var duplicate = await _repository.GetByNameIgnoreCaseAsync(definition.Name);
        if (duplicate is not null && duplicate.Id != existingId)
        {
            throw new ArgumentException("A role with this name already exists.");
        }

        var invalidPermission = definition.Permissions.FirstOrDefault(permission =>
            !PermissionCatalog.Contains(permission, StringComparer.OrdinalIgnoreCase));
        if (invalidPermission is not null)
        {
            throw new ArgumentException($"Permission \"{invalidPermission}\" is not supported.");
        }
    }

    private static RoleDefinition Normalize(RoleDefinition definition)
    {
        var name = definition.Name.Trim();
        return new RoleDefinition
        {
            Name = name,
            NameNormalized = name.ToUpperInvariant(),
            Description = string.IsNullOrWhiteSpace(definition.Description)
                ? null
                : definition.Description.Trim(),
            Permissions = definition.Permissions
                .Where(permission => !string.IsNullOrWhiteSpace(permission))
                .Select(permission => permission.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList(),
            IsEnabled = definition.IsEnabled,
            CreatedDateUtc = definition.CreatedDateUtc == default
                ? DateTime.UtcNow
                : definition.CreatedDateUtc,
            LastModifiedDateUtc = definition.LastModifiedDateUtc,
        };
    }
}
