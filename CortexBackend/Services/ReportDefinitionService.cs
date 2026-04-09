using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Cortex.API.Data.Repositories;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public partial class ReportDefinitionService(
    IReportDefinitionRepository repository,
    CortexDbContext context) : IReportDefinitionService
{
    private const int MaxRows = 500;

    private readonly IReportDefinitionRepository _repository = repository;
    private readonly CortexDbContext _context = context;

    [GeneratedRegex("^\\s*(SELECT|WITH)\\b", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex ReadOnlyQueryPattern();

    [GeneratedRegex("\\b(INSERT|UPDATE|DELETE|MERGE|DROP|ALTER|CREATE|TRUNCATE|EXEC|EXECUTE|GRANT|REVOKE|DENY|DBCC|BACKUP|RESTORE)\\b", RegexOptions.IgnoreCase)]
    private static partial Regex BlockedKeywordPattern();

    public async Task<IReadOnlyList<ReportDefinition>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<ReportDefinition> CreateAsync(ReportDefinition definition)
    {
        var normalized = Normalize(definition);
        await ValidateAsync(normalized, null);

        await _repository.AddAsync(normalized);
        await _repository.SaveChangesAsync();

        return normalized;
    }

    public async Task<ReportDefinition> UpdateAsync(int id, ReportDefinition definition)
    {
        var existing = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Report definition was not found.");

        var normalized = Normalize(definition);
        await ValidateAsync(normalized, id);

        existing.Name = normalized.Name;
        existing.Description = normalized.Description;
        existing.SqlQuery = normalized.SqlQuery;
        existing.IsEnabled = normalized.IsEnabled;
        existing.LastModifiedDateUtc = DateTime.UtcNow;

        await _repository.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        var existing = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Report definition was not found.");

        _repository.Delete(existing);
        await _repository.SaveChangesAsync();
    }

    public async Task<CustomReportResultResponse> ExecuteAsync(int id)
    {
        var definition = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Report definition was not found.");

        if (!definition.IsEnabled)
        {
            throw new InvalidOperationException("This report is currently disabled.");
        }

        await ValidateSqlShapeAsync(definition.SqlQuery);

        var rows = new List<Dictionary<string, object?>>();
        var columns = new List<string>();

        await using var connection = _context.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync();
        }

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

    private async Task ValidateAsync(ReportDefinition definition, int? existingId)
    {
        if (string.IsNullOrWhiteSpace(definition.Name))
        {
            throw new ArgumentException("Report name is required.");
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
    }

    private static Task ValidateSqlShapeAsync(string sqlQuery)
    {
        var normalized = sqlQuery.Trim();

        if (!ReadOnlyQueryPattern().IsMatch(normalized))
        {
            throw new ArgumentException("Custom reports must start with a SELECT statement or CTE.");
        }

        if (normalized.Contains(';'))
        {
            throw new ArgumentException("Custom reports must use a single SQL statement without semicolons.");
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
        return new ReportDefinition
        {
            Name = definition.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(definition.Description)
                ? null
                : definition.Description.Trim(),
            SqlQuery = definition.SqlQuery.Trim(),
            IsEnabled = definition.IsEnabled,
            CreatedDateUtc = definition.CreatedDateUtc == default
                ? DateTime.UtcNow
                : definition.CreatedDateUtc,
            LastModifiedDateUtc = definition.LastModifiedDateUtc
        };
    }
}
