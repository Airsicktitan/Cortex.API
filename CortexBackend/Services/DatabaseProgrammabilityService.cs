using System.Data;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Cortex.API.Database;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Cortex.API.Services;

public partial class DatabaseProgrammabilityService(
    CortexDbContext context,
    ILogger<DatabaseProgrammabilityService> logger,
    IHttpContextAccessor httpContextAccessor)
    : IDatabaseProgrammabilityService
{
    private readonly CortexDbContext _context = context;
    private readonly ILogger<DatabaseProgrammabilityService> _logger = logger;
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*(\\.[A-Za-z_][A-Za-z0-9_]*)?$")]
    private static partial Regex QualifiedObjectNamePattern();

    [GeneratedRegex("^\\s*CREATE(?:\\s+OR\\s+ALTER)?\\s+VIEW\\s+[^\\s]+\\s+AS\\s+(?<body>.+)$", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CreateViewPattern();

    [GeneratedRegex("^\\s*CREATE(?:\\s+OR\\s+ALTER)?\\s+PROC(?:EDURE)?\\s+[^\\s]+\\s+AS\\s+(?<body>.+)$", RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex CreateProcedurePattern();

    public async Task<IReadOnlyList<DatabaseViewDefinition>> GetUserViewsAsync()
    {
        return await QueryDefinitionsAsync(
            """
            SELECT
                CONCAT(QUOTENAME(s.name), '.', QUOTENAME(v.name)) AS ObjectName,
                m.definition AS DefinitionSql
            FROM sys.views v
            INNER JOIN sys.schemas s ON v.schema_id = s.schema_id
            INNER JOIN sys.sql_modules m ON v.object_id = m.object_id
            WHERE v.is_ms_shipped = 0
            ORDER BY s.name, v.name
            """,
            static row => new DatabaseViewDefinition(
                NormalizeQuotedName(row.GetString(0)),
                ExtractBody(CreateViewPattern(), row.GetString(1))));
    }

    public async Task<IReadOnlyList<DatabaseStoredProcedureDefinition>> GetUserStoredProceduresAsync()
    {
        return await QueryDefinitionsAsync(
            """
            SELECT
                CONCAT(QUOTENAME(s.name), '.', QUOTENAME(p.name)) AS ObjectName,
                m.definition AS DefinitionSql
            FROM sys.procedures p
            INNER JOIN sys.schemas s ON p.schema_id = s.schema_id
            INNER JOIN sys.sql_modules m ON p.object_id = m.object_id
            WHERE p.is_ms_shipped = 0
            ORDER BY s.name, p.name
            """,
            static row => new DatabaseStoredProcedureDefinition(
                NormalizeQuotedName(row.GetString(0)),
                ExtractBody(CreateProcedurePattern(), row.GetString(1))));
    }

    public async Task CreateOrAlterViewAsync(string viewName, string definitionSql)
    {
        var qualifiedName = QuoteQualifiedName(viewName);
        AuditDdlExecution("CreateOrAlterView", viewName);
        await ExecuteNonQueryAsync(
            $"""
            CREATE OR ALTER VIEW {qualifiedName}
            AS
            {definitionSql.Trim()}
            """);
    }

    public async Task DropViewAsync(string viewName)
    {
        var (schemaName, objectName) = SplitQualifiedName(viewName);
        AuditDdlExecution("DropView", viewName);
        await ExecuteNonQueryAsync(
            $"""
            IF OBJECT_ID(N'{schemaName}.{objectName}', N'V') IS NOT NULL
                DROP VIEW {QuoteIdentifier(schemaName)}.{QuoteIdentifier(objectName)}
            """);
    }

    public async Task CreateOrAlterStoredProcedureAsync(string procedureName, string definitionSql)
    {
        var qualifiedName = QuoteQualifiedName(procedureName);
        AuditDdlExecution("CreateOrAlterProcedure", procedureName);
        await ExecuteNonQueryAsync(
            $"""
            CREATE OR ALTER PROCEDURE {qualifiedName}
            AS
            {definitionSql.Trim()}
            """);
    }

    public async Task DropStoredProcedureAsync(string procedureName)
    {
        var (schemaName, objectName) = SplitQualifiedName(procedureName);
        AuditDdlExecution("DropProcedure", procedureName);
        await ExecuteNonQueryAsync(
            $"""
            IF OBJECT_ID(N'{schemaName}.{objectName}', N'P') IS NOT NULL
                DROP PROCEDURE {QuoteIdentifier(schemaName)}.{QuoteIdentifier(objectName)}
            """);
    }

    /// <summary>
    /// Emits a structured audit event for any user-driven DDL mutation routed
    /// through this service. Fields are intentionally stable so downstream log
    /// sinks (Serilog, App Insights, etc.) can alert on them. The event is
    /// best-effort — an audit failure must never block the DDL itself.
    /// </summary>
    private void AuditDdlExecution(string operation, string objectName)
    {
        try
        {
            var httpContext = _httpContextAccessor.HttpContext;
            var actorUserId = httpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? httpContext?.User.FindFirst("sub")?.Value
                ?? "system";
            var clientIp = httpContext?.Connection.RemoteIpAddress?.ToString();

            _logger.LogInformation(
                "cortex.ddl.executed Operation={Operation} ObjectName={ObjectName} ActorUserId={ActorUserId} ClientIp={ClientIp} TimestampUtc={TimestampUtc}",
                operation,
                objectName,
                actorUserId,
                clientIp,
                DateTime.UtcNow);
        }
        catch
        {
            // Audit must never block DDL.
        }
    }

    private async Task<IReadOnlyList<T>> QueryDefinitionsAsync<T>(
        string sql,
        Func<IDataRecord, T> projector)
    {
        var definitions = new List<T>();

        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 30;
            AttachCurrentTransaction(command);

            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                definitions.Add(projector(reader));
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }

        return definitions;
    }

    private async Task ExecuteNonQueryAsync(string sql)
    {
        var connection = _context.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync();
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.CommandType = CommandType.Text;
            command.CommandTimeout = 30;
            AttachCurrentTransaction(command);

            await command.ExecuteNonQueryAsync();
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private void AttachCurrentTransaction(IDbCommand command)
    {
        var currentTransaction = _context.Database.CurrentTransaction;
        if (currentTransaction is null)
        {
            return;
        }

        command.Transaction = currentTransaction.GetDbTransaction();
    }

    public static string NormalizeQualifiedObjectName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            throw new ArgumentException("Database object name is required.");
        }

        var normalized = objectName.Trim()
            .Replace("[", string.Empty, StringComparison.Ordinal)
            .Replace("]", string.Empty, StringComparison.Ordinal);

        if (!QualifiedObjectNamePattern().IsMatch(normalized))
        {
            throw new ArgumentException(
                "Database object names must use letters, numbers, underscores, and an optional schema prefix.");
        }

        return normalized.Contains('.', StringComparison.Ordinal)
            ? normalized
            : $"dbo.{normalized}";
    }

    public static string QuoteQualifiedName(string objectName)
    {
        var (schemaName, name) = SplitQualifiedName(objectName);
        return $"{QuoteIdentifier(schemaName)}.{QuoteIdentifier(name)}";
    }

    private static (string SchemaName, string ObjectName) SplitQualifiedName(string objectName)
    {
        var normalized = NormalizeQualifiedObjectName(objectName);
        var parts = normalized.Split('.', 2, StringSplitOptions.TrimEntries);
        return parts.Length == 2
            ? (parts[0], parts[1])
            : ("dbo", parts[0]);
    }

    private static string QuoteIdentifier(string name)
    {
        return $"[{name.Replace("]", "]]", StringComparison.Ordinal)}]";
    }

    private static string NormalizeQuotedName(string quotedName)
    {
        return quotedName
            .Replace("[", string.Empty, StringComparison.Ordinal)
            .Replace("]", string.Empty, StringComparison.Ordinal);
    }

    private static string ExtractBody(Regex pattern, string definitionSql)
    {
        var match = pattern.Match(definitionSql.Trim());
        return match.Success
            ? match.Groups["body"].Value.Trim()
            : definitionSql.Trim();
    }
}
