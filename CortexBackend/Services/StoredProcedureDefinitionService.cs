using System.Text.RegularExpressions;
using Cortex.API.Data.Repositories;
using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public partial class StoredProcedureDefinitionService(
    IStoredProcedureDefinitionRepository repository,
    CortexDbContext context,
    IDatabaseProgrammabilityService programmabilityService) : IStoredProcedureDefinitionService
{
    private readonly IStoredProcedureDefinitionRepository _repository = repository;
    private readonly CortexDbContext _context = context;
    private readonly IDatabaseProgrammabilityService _programmabilityService = programmabilityService;

    // Blocks T-SQL that manipulates server/database config, grants permissions,
    // executes dynamic SQL, reaches external sources, or runs extended procs.
    // INSERT/UPDATE/DELETE/MERGE/SELECT are intentionally allowed because this
    // is a stored-procedure body, not a read-only report view.
    [GeneratedRegex(
        @"\b(?:xp_[A-Za-z0-9_]+|sp_configure|sp_executesql|GRANT|DENY|REVOKE|OPENROWSET|OPENDATASOURCE|OPENQUERY|BULK\s+INSERT|BACKUP|RESTORE|SHUTDOWN|RECONFIGURE|DBCC)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex BlockedStoredProcedureKeywordPattern();

    // Blocks dynamic SQL execution (EXEC('...') / EXECUTE('...') / EXEC @var).
    // EXEC dbo.SomeProc is allowed; EXEC(<expression>) is not.
    [GeneratedRegex(
        @"\b(?:EXEC|EXECUTE)\s*(?:\(|@)",
        RegexOptions.IgnoreCase)]
    private static partial Regex BlockedDynamicExecPattern();

    // Blocks DROP / TRUNCATE / CREATE/ALTER targeting sensitive schema objects
    // (logins, users, roles, databases, servers, credentials, certs, schemas).
    // We allow ALTER TABLE/INDEX/PROCEDURE/FUNCTION/VIEW/TRIGGER for legitimate
    // maintenance inside a procedure body.
    [GeneratedRegex(
        @"\b(?:DROP|TRUNCATE)\b|\b(?:CREATE|ALTER)\s+(?:DATABASE|SERVER|LOGIN|USER|ROLE|SCHEMA|CREDENTIAL|CERTIFICATE|MASTER\s+KEY|APPLICATION\s+ROLE|ENDPOINT|ASSEMBLY)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex BlockedDdlPattern();

    // Blocks SQL Server four-part linked-server names: [srv].[db].[schema].[obj]
    // or srv.db.schema.obj. Three-part names (db.schema.obj) are still allowed.
    [GeneratedRegex(
        @"(?:\[[^\]]+\]|[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*(?:\[[^\]]+\]|[A-Za-z_][A-Za-z0-9_]*)?\s*\.\s*(?:\[[^\]]+\]|[A-Za-z_][A-Za-z0-9_]*)\s*\.\s*(?:\[[^\]]+\]|[A-Za-z_][A-Za-z0-9_]*)")]
    private static partial Regex FourPartNamePattern();

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
        try
        {
            await _programmabilityService.CreateOrAlterStoredProcedureAsync(
                normalized.ProcedureName,
                normalized.DefinitionSql);
        }
        catch (SqlException exception)
        {
            throw new ArgumentException(
                $"SQL Server could not create the stored procedure '{normalized.ProcedureName}'. {exception.Message}",
                nameof(definition),
                exception);
        }

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
        try
        {
            await _programmabilityService.CreateOrAlterStoredProcedureAsync(
                normalized.ProcedureName,
                normalized.DefinitionSql);
        }
        catch (SqlException exception)
        {
            throw new ArgumentException(
                $"SQL Server could not update the stored procedure '{normalized.ProcedureName}'. {exception.Message}",
                nameof(definition),
                exception);
        }

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
        var referencingJobs = await _context.ScheduledJobs
            .Where(job => job.StoredProcedureDefinitionId == id)
            .ToListAsync();

        await using var transaction = await _context.Database.BeginTransactionAsync();

        if (referencingJobs.Count > 0)
        {
            var utcNow = DateTime.UtcNow;
            var definitionLabel = string.IsNullOrWhiteSpace(existing.Name)
                ? existing.ProcedureName
                : existing.Name;

            foreach (var job in referencingJobs)
            {
                job.StoredProcedureDefinitionId = null;
                job.IsEnabled = false;
                job.LastModifiedDateUtc = utcNow;
                job.NextRunDateUtc = null;
                job.LastRunStatus = "Failed";
                job.LastRunMessage =
                    $"Stored procedure \"{definitionLabel}\" was deleted. Select a replacement procedure before re-enabling this job.";
            }
        }

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

        ValidateProcedureBodyShape(definition.DefinitionSql);

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

    /// <summary>
    /// Enforces a deliberately narrow safety envelope on the body of a stored
    /// procedure definition. Intentionally blocks: extended procs (xp_*),
    /// server/db configuration (sp_configure, RECONFIGURE, SHUTDOWN), permission
    /// changes (GRANT/DENY/REVOKE), ad-hoc distributed queries
    /// (OPENROWSET/OPENDATASOURCE/OPENQUERY, BULK INSERT), dynamic SQL
    /// (EXEC('...'), EXECUTE(@var), sp_executesql), linked-server four-part
    /// names, DROP/TRUNCATE, and CREATE/ALTER against sensitive server objects
    /// (logins, users, roles, schemas, databases, credentials, certificates,
    /// endpoints, assemblies). This is a pragmatic keyword-level guard, not a
    /// full T-SQL parser — see Microsoft.SqlServer.TransactSql.ScriptDom for a
    /// future upgrade path.
    /// </summary>
    internal static void ValidateProcedureBodyShape(string definitionSql)
    {
        var normalized = definitionSql.Trim();

        if (normalized.Length == 0)
        {
            throw new ArgumentException("Procedure SQL definition is required.");
        }

        if (BlockedStoredProcedureKeywordPattern().IsMatch(normalized))
        {
            throw new ArgumentException(
                "Stored procedure body contains disallowed keywords "
                + "(extended procs, sp_configure, GRANT/DENY/REVOKE, OPENROWSET/OPENDATASOURCE, "
                + "BULK INSERT, BACKUP/RESTORE, SHUTDOWN, RECONFIGURE, or DBCC).");
        }

        if (BlockedDynamicExecPattern().IsMatch(normalized))
        {
            throw new ArgumentException(
                "Stored procedure body may not execute dynamic SQL "
                + "(EXEC('...'), EXECUTE(@var), or sp_executesql).");
        }

        if (BlockedDdlPattern().IsMatch(normalized))
        {
            throw new ArgumentException(
                "Stored procedure body may not issue DROP/TRUNCATE or "
                + "CREATE/ALTER against servers, databases, logins, users, roles, "
                + "schemas, credentials, certificates, endpoints, or assemblies.");
        }

        if (FourPartNamePattern().IsMatch(normalized))
        {
            throw new ArgumentException(
                "Stored procedure body may not reference linked-server "
                + "four-part object names (server.database.schema.object).");
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
