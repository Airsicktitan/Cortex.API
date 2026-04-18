using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;

namespace Cortex.API;

/// <summary>
/// Startup-time database failures (migrations, seeding) are logged here so the host can continue in degraded mode.
/// </summary>
internal static class StartupDatabaseResilience
{
    /// <summary>
    /// Azure SQL / free-tier database paused when monthly allowance is exhausted (see error 42119).
    /// </summary>
    internal const int AzureSqlFreeTierPausedErrorNumber = 42119;

    internal static void LogStartupDatabaseFailure(
        Exception exception,
        ILogger logger,
        string operation)
    {
        var sqlEx = FindSqlException(exception);
        if (sqlEx?.Number == AzureSqlFreeTierPausedErrorNumber)
        {
            logger.LogWarning(
                exception,
                "Azure SQL is paused due to free-tier limit or the database is unavailable (SqlException {ErrorNumber}). Continuing in degraded mode. Operation: {Operation}",
                sqlEx.Number,
                operation);
            return;
        }

        logger.LogError(
            exception,
            "Database unavailable at startup. Continuing in degraded mode. Operation: {Operation}",
            operation);
    }

    private static SqlException? FindSqlException(Exception exception)
    {
        var stack = new Stack<Exception>();
        stack.Push(exception);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current is SqlException sql)
            {
                return sql;
            }

            if (current is AggregateException aggregate)
            {
                foreach (var inner in aggregate.InnerExceptions)
                {
                    stack.Push(inner);
                }
            }

            if (current.InnerException is { } next)
            {
                stack.Push(next);
            }
        }

        return null;
    }
}
