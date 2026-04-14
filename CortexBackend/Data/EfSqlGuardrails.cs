using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Cortex.API.Services;

namespace Cortex.API.Data;

/// <summary>
/// Single place for EF Core <see cref="DatabaseFacade.ExecuteSqlRawAsync(string, object[])"/> usage.
/// Prefer LINQ and <see cref="DatabaseFacade.ExecuteSqlInterpolatedAsync"/> elsewhere.
/// Dynamic fragments must be validated identifiers (see Cortex.API.Services.DatabaseProgrammabilityService) or parameters.
/// </summary>
internal static class EfSqlGuardrails
{
    /// <summary>Runs a stored procedure after normalizing and bracket-quoting its identifier.</summary>
    public static Task<int> ExecuteStoredProcedureByNameAsync(
        DatabaseFacade database,
        string procedureName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(procedureName);
        var normalizedName = DatabaseProgrammabilityService.NormalizeQualifiedObjectName(procedureName);
        var qualifiedName = DatabaseProgrammabilityService.QuoteQualifiedName(normalizedName);

#pragma warning disable EF1002 // Identifier is validated + bracket-quoted upstream; not concatenated user SQL.
        return database.ExecuteSqlRawAsync($"EXEC {qualifiedName}", cancellationToken);
#pragma warning restore EF1002
    }

    /// <summary>Fixed procedure with only parameter values — no dynamic SQL text.</summary>
    public static Task<int> ExecuteArchiveTicketAsync(
        DatabaseFacade database,
        string ticketId,
        int archivedBy,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ticketId);

        var ticketIdParameter = new SqlParameter("TicketId", ticketId);
        var archivedByParameter = new SqlParameter("ArchivedBy", archivedBy);

        return database.ExecuteSqlRawAsync(
            "EXEC dbo.ArchiveTicket @TicketId, @ArchivedBy",
            new object[] { ticketIdParameter, archivedByParameter },
            cancellationToken);
    }

    /// <summary>Fixed bootstrap script with no user input; keep in sync with schema.</summary>
    public static Task<int> EnsureLegacyUserExistsAsync(
        DatabaseFacade database,
        CancellationToken cancellationToken = default)
    {
        return database.ExecuteSqlRawAsync(
            """
            IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Id = 0)
            BEGIN
                SET IDENTITY_INSERT dbo.Users ON;

                INSERT INTO dbo.Users
                (
                    Id,
                    DisplayName,
                    Email,
                    Role,
                    Department,
                    CreatedDate,
                    IsActive
                )
                VALUES
                (
                    0,
                    N'Legacy User',
                    N'legacy-user@local.invalid',
                    N'User',
                    NULL,
                    SYSUTCDATETIME(),
                    0
                );

                SET IDENTITY_INSERT dbo.Users OFF;
            END;
            """,
            cancellationToken);
    }
}
