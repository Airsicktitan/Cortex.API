using Cortex.API.Data.Repositories;
using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public class TicketStatusService(
    ITicketStatusDefinitionRepository repository,
    CortexDbContext context) : ITicketStatusService
{
    private readonly ITicketStatusDefinitionRepository _repository = repository;
    private readonly CortexDbContext _context = context;

    public async Task<IReadOnlyList<TicketStatusDefinition>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<IReadOnlyList<TicketStatusDefinition>> GetEnabledAsync()
    {
        var definitions = await _repository.GetAllAsync();
        return definitions
            .Where(definition => definition.IsEnabled)
            .ToList();
    }

    public async Task<TicketStatusDefinition> CreateAsync(TicketStatusDefinition definition)
    {
        var normalized = Normalize(definition);
        await ValidateAsync(normalized, null);

        await _repository.AddAsync(normalized);
        await _repository.SaveChangesAsync();

        return normalized;
    }

    public async Task<TicketStatusDefinition> UpdateAsync(int id, TicketStatusDefinition definition)
    {
        var existing = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Ticket status was not found.");

        var normalized = Normalize(definition);
        await ValidateAsync(normalized, id);

        var originalName = existing.Name;
        var isRenamed = !string.Equals(originalName, normalized.Name, StringComparison.OrdinalIgnoreCase);

        if (isRenamed)
        {
            var activeTickets = await _context.Tickets
                .Where(ticket => ticket.Status == originalName)
                .ToListAsync();

            foreach (var ticket in activeTickets)
            {
                ticket.Status = normalized.Name;
            }

            var archivedTickets = await _context.ArchivedTickets
                .Where(ticket => ticket.Status == originalName)
                .ToListAsync();

            foreach (var ticket in archivedTickets)
            {
                ticket.Status = normalized.Name;
            }

            var archiveConfigurations = await _context.ArchiveConfigurations.ToListAsync();
            foreach (var configuration in archiveConfigurations)
            {
                if (!configuration.EligibleStatuses.Any(status =>
                        string.Equals(status, originalName, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                configuration.EligibleStatuses = configuration.EligibleStatuses
                    .Select(status =>
                        string.Equals(status, originalName, StringComparison.OrdinalIgnoreCase)
                            ? normalized.Name
                            : status)
                    .ToList();
            }
        }

        existing.Name = normalized.Name;
        existing.Description = normalized.Description;
        existing.IsEnabled = normalized.IsEnabled;
        existing.LastModifiedDateUtc = DateTime.UtcNow;

        await _repository.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        var existing = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Ticket status was not found.");

        var statusName = existing.Name;
        var isReferencedByTickets = await _context.Tickets.AnyAsync(ticket => ticket.Status == statusName)
            || await _context.ArchivedTickets.AnyAsync(ticket => ticket.Status == statusName);

        if (isReferencedByTickets)
        {
            throw new InvalidOperationException(
                "This status is already used by one or more tickets. Disable it instead of deleting it.");
        }

        var archiveConfigurations = await _context.ArchiveConfigurations.ToListAsync();
        var isReferencedByArchivePolicies = archiveConfigurations.Any(configuration =>
            configuration.EligibleStatuses.Any(status =>
                string.Equals(status, statusName, StringComparison.OrdinalIgnoreCase)));

        if (isReferencedByArchivePolicies)
        {
            throw new InvalidOperationException(
                "This status is still used by an archive policy. Remove it from those policies or disable it instead.");
        }

        _repository.Delete(existing);
        await _repository.SaveChangesAsync();
    }

    public async Task EnsureSelectableStatusAsync(string statusName)
    {
        if (string.IsNullOrWhiteSpace(statusName))
        {
            throw new ArgumentException("Ticket status is required.");
        }

        var normalizedStatusName = statusName.Trim();
        var definition = await _repository.GetByNameAsync(normalizedStatusName);

        if (definition is null)
        {
            throw new ArgumentException($"The ticket status \"{normalizedStatusName}\" is not registered.");
        }

        if (!definition.IsEnabled)
        {
            throw new ArgumentException($"The ticket status \"{normalizedStatusName}\" is currently disabled.");
        }
    }

    public async Task<string> GetDefaultCreateStatusAsync()
    {
        var enabledStatuses = await GetEnabledAsync();

        return enabledStatuses.FirstOrDefault(definition =>
                string.Equals(definition.Name, "New", StringComparison.OrdinalIgnoreCase))
                ?.Name
            ?? enabledStatuses.FirstOrDefault()?.Name
            ?? "New";
    }

    public async Task<string> GetReactivatedStatusAsync(string archivedStatus)
    {
        var enabledStatuses = await GetEnabledAsync();
        var enabledNames = enabledStatuses
            .Select(definition => definition.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (enabledNames.Contains("In Progress"))
        {
            return enabledStatuses.First(definition =>
                string.Equals(definition.Name, "In Progress", StringComparison.OrdinalIgnoreCase)).Name;
        }

        if (enabledNames.Contains("New"))
        {
            return enabledStatuses.First(definition =>
                string.Equals(definition.Name, "New", StringComparison.OrdinalIgnoreCase)).Name;
        }

        if (enabledNames.Contains(archivedStatus))
        {
            return enabledStatuses.First(definition =>
                string.Equals(definition.Name, archivedStatus, StringComparison.OrdinalIgnoreCase)).Name;
        }

        return enabledStatuses.FirstOrDefault()?.Name ?? archivedStatus;
    }

    public async Task<IReadOnlyCollection<string>> GetKnownStatusNamesAsync()
    {
        var definitions = await _repository.GetAllAsync();
        return definitions
            .Select(definition => definition.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private async Task ValidateAsync(TicketStatusDefinition definition, int? existingId)
    {
        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            throw new ArgumentException("Status name is required.");
        }

        var duplicate = await _repository.GetByNameAsync(definition.Name);
        if (duplicate is not null && duplicate.Id != existingId)
        {
            throw new ArgumentException("A ticket status with this name already exists.");
        }
    }

    private static TicketStatusDefinition Normalize(TicketStatusDefinition definition)
    {
        return new TicketStatusDefinition
        {
            Name = definition.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(definition.Description)
                ? null
                : definition.Description.Trim(),
            IsEnabled = definition.IsEnabled,
            CreatedDateUtc = definition.CreatedDateUtc == default
                ? DateTime.UtcNow
                : definition.CreatedDateUtc,
            LastModifiedDateUtc = definition.LastModifiedDateUtc
        };
    }
}
