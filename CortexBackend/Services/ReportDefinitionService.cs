using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Cortex.API.Data.Repositories;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public partial class ReportDefinitionService(
    IReportDefinitionRepository repository,
    CortexDbContext context,
    IDatabaseProgrammabilityService programmabilityService) : IReportDefinitionService
{
    private const int MaxRows = 500;

    private readonly IReportDefinitionRepository _repository = repository;
    private readonly CortexDbContext _context = context;
    private readonly IDatabaseProgrammabilityService _programmabilityService = programmabilityService;

    [GeneratedRegex("^\\s*(SELECT|WITH)\\b", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ReadOnlyQueryPattern();

    [GeneratedRegex("^\\s*CREATE(?:\\s+OR\\s+ALTER)?\\s+VIEW\\s+(?<viewName>(?:\\[[^\\]]+\\]|[A-Za-z_][A-Za-z0-9_]*)(?:\\.(?:\\[[^\\]]+\\]|[A-Za-z_][A-Za-z0-9_]*))?)\\s+AS\\s+(?<body>.+)$", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CreateViewPattern();

    [GeneratedRegex("\\b(INSERT|UPDATE|DELETE|MERGE|DROP|ALTER|CREATE|TRUNCATE|EXEC|EXECUTE|GRANT|REVOKE|DENY|DBCC|BACKUP|RESTORE)\\b", RegexOptions.IgnoreCase)]
    private static partial Regex BlockedKeywordPattern();

    public async Task<IReadOnlyList<ReportDefinition>> GetAllAsync()
    {
        var definitions = (await _repository.GetAllAsync()).ToList();
        await SyncDefinitionsFromDatabaseAsync(definitions);
        return definitions;
    }

    public async Task<IReadOnlyList<DatabaseViewDefinition>> GetAvailableViewsAsync()
    {
        var definitions = await _repository.GetAllAsync();
        var registeredViewNames = definitions
            .Where(definition => !string.IsNullOrWhiteSpace(definition.ViewName))
            .Select(definition => DatabaseProgrammabilityService.NormalizeQualifiedObjectName(definition.ViewName))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var availableViews = await _programmabilityService.GetUserViewsAsync();
        return availableViews
            .Where(view => !registeredViewNames.Contains(
                DatabaseProgrammabilityService.NormalizeQualifiedObjectName(view.ViewName)))
            .ToList();
    }

    public async Task<ReportDefinition> CreateAsync(ReportDefinition definition)
    {
        var normalized = Normalize(definition);
        await ValidateAsync(normalized, null);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _programmabilityService.CreateOrAlterViewAsync(normalized.ViewName, normalized.SqlQuery);
        }
        catch (SqlException exception)
        {
            throw new ArgumentException(
                $"SQL Server could not create the view '{normalized.ViewName}'. {exception.Message}",
                nameof(definition),
                exception);
        }

        await _repository.AddAsync(normalized);
        await _repository.SaveChangesAsync();
        await transaction.CommitAsync();

        return normalized;
    }

    public async Task<ReportDefinition> UpdateAsync(int id, ReportDefinition definition)
    {
        var existing = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Report definition was not found.");

        var originalViewName = existing.ViewName;
        var normalized = Normalize(definition);
        await ValidateAsync(normalized, id);

        await using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            await _programmabilityService.CreateOrAlterViewAsync(normalized.ViewName, normalized.SqlQuery);
        }
        catch (SqlException exception)
        {
            throw new ArgumentException(
                $"SQL Server could not update the view '{normalized.ViewName}'. {exception.Message}",
                nameof(definition),
                exception);
        }

        existing.Name = normalized.Name;
        existing.ViewName = normalized.ViewName;
        existing.Description = normalized.Description;
        existing.SqlQuery = normalized.SqlQuery;
        existing.IsEnabled = normalized.IsEnabled;
        existing.LastModifiedDateUtc = DateTime.UtcNow;

        await _repository.SaveChangesAsync();

        if (!string.IsNullOrWhiteSpace(originalViewName)
            && !string.Equals(originalViewName, normalized.ViewName, StringComparison.OrdinalIgnoreCase))
        {
            await _programmabilityService.DropViewAsync(originalViewName);
        }

        await transaction.CommitAsync();
        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        var existing = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Report definition was not found.");

        await using var transaction = await _context.Database.BeginTransactionAsync();
        if (!string.IsNullOrWhiteSpace(existing.ViewName))
        {
            await _programmabilityService.DropViewAsync(existing.ViewName);
        }

        _repository.Delete(existing);
        await _repository.SaveChangesAsync();
        await transaction.CommitAsync();
    }

    public async Task<CustomReportResultResponse> ExecuteAsync(int id)
    {
        var definition = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Report definition was not found.");

        await SyncDefinitionFromDatabaseAsync(definition);

        if (!definition.IsEnabled)
        {
            throw new InvalidOperationException("This report is currently disabled.");
        }

        await ValidateSqlShapeAsync(definition.SqlQuery);

        var rows = new List<Dictionary<string, object?>>();
        var columns = new List<string>();

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = definition.SqlQuery;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 30;

            await using var reader = await command.ExecuteReaderAsync();

            for (var index = 0; index < reader.FieldCount; index++)
            {
                columns.Add(reader.GetName(index));
            }

            var isTruncated = false;
            while (await reader.ReadAsync())
            {
                if (rows.Count >= MaxRows)
                {
                    isTruncated = true;
                    break;
                }

                var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (var index = 0; index < reader.FieldCount; index++)
                {
                    row[columns[index]] = reader.IsDBNull(index)
                        ? null
                        : NormalizeCellValue(reader.GetValue(index));
                }

                rows.Add(row);
            }

            return new CustomReportResultResponse
            {
                ReportName = definition.Name,
                Columns = columns,
                Rows = rows,
                GeneratedDateUtc = DateTime.UtcNow,
                IsTruncated = isTruncated
            };
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task SyncDefinitionFromDatabaseAsync(ReportDefinition definition)
    {
        if (string.IsNullOrWhiteSpace(definition.ViewName))
        {
            return;
        }

        var availableViews = await _programmabilityService.GetUserViewsAsync();
        var databaseView = availableViews.FirstOrDefault(view =>
            string.Equals(
                DatabaseProgrammabilityService.NormalizeQualifiedObjectName(view.ViewName),
                DatabaseProgrammabilityService.NormalizeQualifiedObjectName(definition.ViewName),
                StringComparison.OrdinalIgnoreCase));

        if (databaseView is null)
        {
            return;
        }

        if (!string.Equals(definition.SqlQuery, databaseView.DefinitionSql, StringComparison.Ordinal))
        {
            definition.SqlQuery = databaseView.DefinitionSql;
            definition.LastModifiedDateUtc = DateTime.UtcNow;
            await _repository.SaveChangesAsync();
        }
    }

    private async Task SyncDefinitionsFromDatabaseAsync(List<ReportDefinition> definitions)
    {
        var availableViews = await _programmabilityService.GetUserViewsAsync();
        var viewMap = availableViews.ToDictionary(
            view => DatabaseProgrammabilityService.NormalizeQualifiedObjectName(view.ViewName),
            StringComparer.OrdinalIgnoreCase);

        var changed = false;
        foreach (var definition in definitions)
        {
            if (string.IsNullOrWhiteSpace(definition.ViewName))
            {
                if (string.IsNullOrWhiteSpace(definition.SqlQuery))
                {
                    continue;
                }

                var generatedViewName = $"dbo.vw_CortexReport_{definition.Id}";
                try
                {
                    await _programmabilityService.CreateOrAlterViewAsync(
                        generatedViewName,
                        definition.SqlQuery);
                    definition.ViewName = generatedViewName;
                    definition.LastModifiedDateUtc = DateTime.UtcNow;
                    changed = true;
                }
                catch
                {
                    // Leave legacy records readable even if their SQL cannot be materialized as a view.
                }

                continue;
            }

            var normalizedViewName = DatabaseProgrammabilityService.NormalizeQualifiedObjectName(definition.ViewName);
            if (!string.Equals(definition.ViewName, normalizedViewName, StringComparison.Ordinal))
            {
                definition.ViewName = normalizedViewName;
                changed = true;
            }

            if (!viewMap.TryGetValue(normalizedViewName, out var databaseView))
            {
                continue;
            }

            if (!string.Equals(definition.SqlQuery, databaseView.DefinitionSql, StringComparison.Ordinal))
            {
                definition.SqlQuery = databaseView.DefinitionSql;
                definition.LastModifiedDateUtc = DateTime.UtcNow;
                changed = true;
            }
        }

        if (changed)
        {
            await _repository.SaveChangesAsync();
        }
    }

    private async Task ValidateAsync(ReportDefinition definition, int? existingId)
    {
        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            throw new ArgumentException("Report name is required.");
        }

        if (string.IsNullOrWhiteSpace(definition.ViewName))
        {
            throw new ArgumentException("View name is required.");
        }

        if (string.IsNullOrWhiteSpace(definition.SqlQuery))
        {
            throw new ArgumentException("SQL query is required.");
        }

        await ValidateSqlShapeAsync(definition.SqlQuery);

        var duplicateName = await _repository.GetByNameAsync(definition.Name);
        if (duplicateName is not null && duplicateName.Id != existingId)
        {
            throw new ArgumentException("A report with this name already exists.");
        }

        var duplicateViewName = await _repository.GetByViewNameAsync(definition.ViewName);
        if (duplicateViewName is not null && duplicateViewName.Id != existingId)
        {
            throw new ArgumentException("A report with this view name already exists.");
        }
    }

    private static Task ValidateSqlShapeAsync(string sqlQuery)
    {
        var normalized = sqlQuery.Trim();

        if (!ReadOnlyQueryPattern().IsMatch(normalized))
        {
            throw new ArgumentException("Custom report views must start with a SELECT statement or CTE.");
        }

        if (normalized.Contains(';'))
        {
            throw new ArgumentException("Custom report views must use a single SQL statement without semicolons.");
        }

        if (BlockedKeywordPattern().IsMatch(normalized))
        {
            throw new ArgumentException("Custom reports only support read-only SQL.");
        }

        return Task.CompletedTask;
    }

    private static object? NormalizeCellValue(object? value)
    {
        return value switch
        {
            null => null,
            DBNull => null,
            byte[] => "[binary]",
            DateTime dateTime => dateTime,
            DateTimeOffset dateTimeOffset => dateTimeOffset,
            bool boolean => boolean,
            byte number => number,
            short number => number,
            int number => number,
            long number => number,
            float number => number,
            double number => number,
            decimal number => number,
            Guid guid => guid.ToString(),
            TimeSpan timeSpan => timeSpan.ToString(),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.ToString()
        };
    }

    private static ReportDefinition Normalize(ReportDefinition definition)
    {
        var normalizedName = definition.Name.Trim();
        var normalizedSqlQuery = NormalizeSqlQuery(definition.SqlQuery);
        var viewNameFromSql = TryExtractViewNameFromSql(definition.SqlQuery);
        var rawViewName = string.IsNullOrWhiteSpace(definition.ViewName)
            ? viewNameFromSql ?? GenerateDefaultViewName(normalizedName)
            : definition.ViewName;

        return new ReportDefinition
        {
            Name = normalizedName,
            ViewName = DatabaseProgrammabilityService.NormalizeQualifiedObjectName(rawViewName),
            Description = string.IsNullOrWhiteSpace(definition.Description)
                ? null
                : definition.Description.Trim(),
            SqlQuery = normalizedSqlQuery,
            IsEnabled = definition.IsEnabled,
            CreatedDateUtc = definition.CreatedDateUtc == default
                ? DateTime.UtcNow
                : definition.CreatedDateUtc,
            LastModifiedDateUtc = definition.LastModifiedDateUtc
        };
    }

    private static string NormalizeSqlQuery(string sqlQuery)
    {
        var normalized = sqlQuery.Trim();
        var createViewMatch = CreateViewPattern().Match(normalized);
        if (createViewMatch.Success)
        {
            normalized = createViewMatch.Groups["body"].Value.Trim();
        }

        if (normalized.StartsWith(";WITH", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized[1..].TrimStart();
        }

        normalized = normalized.TrimEnd();
        while (normalized.EndsWith(';'))
        {
            normalized = normalized[..^1].TrimEnd();
        }

        return normalized;
    }

    private static string? TryExtractViewNameFromSql(string sqlQuery)
    {
        var match = CreateViewPattern().Match(sqlQuery.Trim());
        if (!match.Success)
        {
            return null;
        }

        var rawViewName = match.Groups["viewName"].Value;
        return rawViewName
            .Replace("[", string.Empty, StringComparison.Ordinal)
            .Replace("]", string.Empty, StringComparison.Ordinal);
    }

    private static string GenerateDefaultViewName(string reportName)
    {
        var slug = Regex.Replace(reportName.Trim(), "[^A-Za-z0-9_]+", "_");
        slug = Regex.Replace(slug, "_{2,}", "_").Trim('_');

        if (string.IsNullOrWhiteSpace(slug))
        {
            slug = "CustomReport";
        }

        if (char.IsDigit(slug[0]))
        {
            slug = $"Report_{slug}";
        }

        return $"dbo.vw_CortexReport_{slug}";
    }
}
