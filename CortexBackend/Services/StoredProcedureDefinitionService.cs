using System.Text.RegularExpressions;
using Cortex.API.Data.Repositories;
using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public partial class StoredProcedureDefinitionService(
    IStoredProcedureDefinitionRepository repository,
    CortexDbContext context) : IStoredProcedureDefinitionService
{
    private readonly IStoredProcedureDefinitionRepository _repository = repository;
    private readonly CortexDbContext _context = context;

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*(\\.[A-Za-z_][A-Za-z0-9_]*)?$")]
    private static partial Regex ProcedureNamePattern();

    public async Task<IReadOnlyList<StoredProcedureDefinition>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<StoredProcedureDefinition> CreateAsync(StoredProcedureDefinition definition)
    {
        var normalized = Normalize(definition);
        await ValidateAsync(normalized, null);

        await _repository.AddAsync(normalized);
        await _repository.SaveChangesAsync();

        return normalized;
    }

    public async Task<StoredProcedureDefinition> UpdateAsync(int id, StoredProcedureDefinition definition)
    {
        var existing = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Stored procedure definition was not found.");

        var normalized = Normalize(definition);
        await ValidateAsync(normalized, id);

        existing.Name = normalized.Name;
        existing.ProcedureName = normalized.ProcedureName;
        existing.Description = normalized.Description;
        existing.IsEnabled = normalized.IsEnabled;
        existing.LastModifiedDateUtc = DateTime.UtcNow;

        await _repository.SaveChangesAsync();
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

        _repository.Delete(existing);
        await _repository.SaveChangesAsync();
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

        if (!ProcedureNamePattern().IsMatch(definition.ProcedureName))
        {
            throw new ArgumentException("Procedure names must use letters, numbers, underscores, and an optional schema prefix.");
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
            ProcedureName = definition.ProcedureName.Trim(),
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
