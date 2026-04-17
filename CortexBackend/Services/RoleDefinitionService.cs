using Cortex.API.Data.Repositories;
using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public class RoleDefinitionService(
    IRoleDefinitionRepository repository,
    CortexDbContext context) : IRoleDefinitionService
{
    private readonly IRoleDefinitionRepository _repository = repository;
    private readonly CortexDbContext _context = context;
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
            .Where(user => user.Role == existing.Name)
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

    private async Task ValidateAsync(RoleDefinition definition, int? existingId)
    {
        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            throw new ArgumentException("Role name is required.");
        }

        if (definition.Permissions.Count == 0)
        {
            throw new ArgumentException("Select at least one permission.");
        }

        var duplicate = await _repository.GetByNameAsync(definition.Name);
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
        return new RoleDefinition
        {
            Name = definition.Name.Trim(),
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
            LastModifiedDateUtc = definition.LastModifiedDateUtc
        };
    }
}
