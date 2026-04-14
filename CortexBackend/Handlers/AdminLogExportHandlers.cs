using System.Globalization;
using System.Text;
using Cortex.API.Data.Repositories;
using Cortex.API.Models;

namespace Cortex.API.Handlers;

public static class AdminLogExportHandlers
{
    public const int MaxExportRows = 50_000;
    public const int MaxDateRangeDays = 31;

    public static async Task<IResult> ExportRequestLogs(
        string? from,
        string? to,
        string? format,
        IHttpRequestLogRepository repository)
    {
        if (!string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { message = "Only CSV export is supported. Use format=csv." });
        }

        if (!TryParseUtcParameter(from, out var fromUtc, out var fromError))
        {
            return Results.BadRequest(new { message = fromError });
        }

        if (!TryParseUtcParameter(to, out var toUtc, out var toError))
        {
            return Results.BadRequest(new { message = toError });
        }

        if (toUtc < fromUtc)
        {
            return Results.BadRequest(new { message = "Parameter 'to' must be greater than or equal to 'from' (UTC)." });
        }

        if ((toUtc - fromUtc).TotalDays > MaxDateRangeDays)
        {
            return Results.BadRequest(new
            {
                message = $"Date range must be at most {MaxDateRangeDays} days."
            });
        }

        var rows = await repository.GetBetweenAsync(fromUtc, toUtc, MaxExportRows);
        var csv = BuildCsv(rows);
        var fileName = $"cortex-request-logs-{fromUtc:yyyyMMdd}-{toUtc:yyyyMMdd}.csv";
        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(csv))
            .ToArray();

        return Results.File(bytes, "text/csv; charset=utf-8", fileName);
    }

    private static bool TryParseUtcParameter(string? value, out DateTime utc, out string errorMessage)
    {
        utc = default;
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(value))
        {
            errorMessage = "Parameters 'from' and 'to' are required (UTC, ISO 8601).";
            return false;
        }

        if (DateTimeOffset.TryParse(
                value.Trim(),
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var parsed))
        {
            utc = parsed.UtcDateTime;
            return true;
        }

        errorMessage = "Invalid date. Use UTC ISO 8601 (for example 2026-04-01T00:00:00Z).";
        return false;
    }

    private static string BuildCsv(IReadOnlyList<HttpRequestLogEntry> rows)
    {
        var builder = new StringBuilder();
        var headers = new[]
        {
            "OccurredUtc",
            "Method",
            "Path",
            "StatusCode",
            "DurationMs",
            "TraceId",
            "IsAuthenticated"
        };

        builder.AppendLine(string.Join(",", headers.Select(EscapeCsv)));

        foreach (var row in rows)
        {
            var values = new[]
            {
                row.OccurredUtc.ToString("O", CultureInfo.InvariantCulture),
                row.Method,
                row.Path,
                row.StatusCode.ToString(CultureInfo.InvariantCulture),
                row.DurationMs.ToString(CultureInfo.InvariantCulture),
                row.TraceId,
                row.IsAuthenticated ? "true" : "false"
            };

            builder.AppendLine(string.Join(",", values.Select(EscapeCsv)));
        }

        return builder.ToString();
    }

    private static string EscapeCsv(string? value)
    {
        var normalizedValue = value ?? string.Empty;
        var escapedValue = normalizedValue.Replace("\"", "\"\"");
        return $"\"{escapedValue}\"";
    }
}
