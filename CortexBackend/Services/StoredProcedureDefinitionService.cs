using Cortex.API.Data.Repositories;
using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public class StoredProcedureDefinitionService(
    IStoredProcedureDefinitionRepository repository,
    CortexDbContext context,
    IDatabaseProgrammabilityService programmabilityService) : IStoredProcedureDefinitionService
{
    private readonly IStoredProcedureDefinitionRepository _repository = repository;
    private readonly CortexDbContext _context = context;
    private readonly IDatabaseProgrammabilityService _programmabilityService = programmabilityService;

    public async Task<IReadOnlyList<StoredProcedureDefinition>> GetAllAsync()
    {
        var definitions = await _repository.GetAllAsync();
        await SyncDefinitionsFromDatabaseAsync(definitions);
        return definitions;
    }

    public async Task<IReadOnlyList<DatabaseStoredProcedureDefinition>> GetAvailableStoredProceduresAsync()
    {
        var definitions = await _repository.GetAllAsync();
        var registeredProcedureNames = definitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition.ProcedureName))
            .Select(definition => DatabaseProgrammabilityService.NormalizeQualifiedObjectName(definition.ProcedureName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var availableStoredProcedures = await _programmabilityService.GetUserStoredProceduresAsync();
        return availableStoredProcedures
            .Where(definition => !registeredProcedureNames.Contains(
                DatabaseProgrammabilityService.NormalizeQualifiedObjectName(definition.ProcedureName)))
            .ToList();
    }

    public async Task<StoredProcedureDefinition> CreateAsync(StoredProcedureDefinition definition)
    {
        var normalized = Normalize(definition);
        await ValidateAsync(normalized, null);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        await _programmabilityService.CreateOrAlterStoredProcedureAsync(
            normalized.ProcedureName,
            normalized.DefinitionSql);
        await _repository.AddAsync(normalized);
        await _repository.SaveChangesAsync();
        await transaction.CommitAsync();

        return normalized;
    }

    public async Task<StoredProcedureDefinition> UpdateAsync(int id, StoredProcedureDefinition definition)
    {
        var existing = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Stored procedure definition was not found.");

        var originalProcedureName = existing.ProcedureName;
        var normalized = Normalize(definition);
        await ValidateAsync(normalized, id);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        await _programmabilityService.CreateOrAlterStoredProcedureAsync(
            normalized.ProcedureName,
            normalized.DefinitionSql);

        existing.Name = normalized.Name;
        existing.ProcedureName = normalized.ProcedureName;
        existing.DefinitionSql = normalized.DefinitionSql;
        existing.Description = normalized.Description;
        existing.IsEnabled = normalized.IsEnabled;
        existing.LastModifiedDateUtc = DateTime.UtcNow;

        await _repository.SaveChangesAsync();

        if (!string.Equals(originalProcedureName, normalized.ProcedureName, StringComparison.OrdinalIgnoreCase))
        {
            await _programmabilityService.DropStoredProcedureAsync(originalProcedureName);
        }

        await transaction.CommitAsync();
        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        var existing = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Stored procedure definition was not found.");

        var isReferencedByJobs = await _context.ScheduledJobs
            .AnyAsync(job => job.StoredProcedureDefinitionId == id);

        if (isReferencedByJobs)
        {
            throw new InvalidOperationException(
                "This stored procedure is assigned to one or more jobs. Remove it from those jobs or disable it instead.");
        }

        await using var transaction = await _context.Database.BeginTransactionAsync();
        if (!string.IsNullOrWhiteSpace(existing.ProcedureName))
        {
            await _programmabilityService.DropStoredProcedureAsync(existing.ProcedureName);
        }

        _repository.Delete(existing);
        await _repository.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    private async Task SyncDefinitionsFromDatabaseAsync(List<StoredProcedureDefinition> definitions)
    {
        var availableStoredProcedures = await _programmabilityService.GetUserStoredProceduresAsync();
        var procedureMap = availableStoredProcedures.ToDictionary(
            definition => DatabaseProgrammabilityService.NormalizeQualifiedObjectName(definition.ProcedureName),
            StringComparer.OrdinalIgnoreCase);

        var changed = false;
        foreach (var definition in definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.ProcedureName))
            {
                continue;
            }

            var normalizedProcedureName = DatabaseProgrammabilityService.NormalizeQualifiedObjectName(definition.ProcedureName);
            if (!string.Equals(definition.ProcedureName, normalizedProcedureName, StringComparison.Ordinal))
            {
                definition.ProcedureName = normalizedProcedureName;
                changed = true;
            }

            if (!procedureMap.TryGetValue(normalizedProcedureName, out var databaseProcedure))
            {
                continue;
            }

            if (!string.Equals(definition.DefinitionSql, databaseProcedure.DefinitionSql, StringComparison.Ordinal))
            {
                definition.DefinitionSql = databaseProcedure.DefinitionSql;
                definition.LastModifiedDateUtc = DateTime.UtcNow;
                changed = true;
            }
        }

        if (changed)
        {
            await _repository.SaveChangesAsync();
        }
    }

    private async Task ValidateAsync(StoredProcedureDefinition definition, int? existingId)
    {
        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            throw new ArgumentException("Stored procedure name label is required.");
        }

        if (string.IsNullOrWhiteSpace(definition.ProcedureName))
        {
            throw new ArgumentException("Procedure name is required.");
        }

        if (string.IsNullOrWhiteSpace(definition.DefinitionSql))
        {
            throw new ArgumentException("Procedure SQL definition is required.");
        }

        var duplicateName = await _repository.GetByNameAsync(definition.Name);
        if (duplicateName is not null && duplicateName.Id != existingId)
        {
            throw new ArgumentException("A stored procedure definition with this label already exists.");
        }

        var duplicateProcedureName = await _repository.GetByProcedureNameAsync(definition.ProcedureName);
        if (duplicateProcedureName is not null && duplicateProcedureName.Id != existingId)
        {
            throw new ArgumentException("This stored procedure has already been registered.");
        }
    }

    private static StoredProcedureDefinition Normalize(StoredProcedureDefinition definition)
    {
        return new StoredProcedureDefinition
        {
            Name = definition.Name.Trim(),
            ProcedureName = DatabaseProgrammabilityService.NormalizeQualifiedObjectName(definition.ProcedureName),
            DefinitionSql = definition.DefinitionSql.Trim(),
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
