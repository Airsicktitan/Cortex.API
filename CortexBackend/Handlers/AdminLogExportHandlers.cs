using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Cortex.API.Data.Repositories;
using Cortex.API.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

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
        var normalizedFormat = string.IsNullOrWhiteSpace(format)
            ? "csv"
            : format.Trim().ToLowerInvariant();

        if (normalizedFormat is not ("csv" or "json" or "txt" or "xlsx" or "sheets"))
        {
            return Results.BadRequest(new { message = "Invalid format. Supported values: csv, json, txt, xlsx, sheets." });
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
        var logs = rows.Select(AdminRequestLogExportRow.FromEntry).ToList();

        var fileStamp = DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var fileName = GetExportFileName(normalizedFormat, fileStamp);

        return normalizedFormat switch
        {
            "csv" => BuildCsvFileResult(logs, fileName),
            "json" => BuildJsonFileResult(logs, fileName),
            "txt" => BuildTextFileResult(logs, fileName),
            "xlsx" => BuildExcelFileResult(logs, fileName),
            "sheets" => BuildExcelFileResult(logs, fileName),
            _ => Results.BadRequest(new { message = "Invalid format. Supported values: csv, json, txt, xlsx, sheets." })
        };
    }

    private static string GetExportFileName(string normalizedFormat, string fileStamp) =>
        normalizedFormat switch
        {
            "csv" => $"cortex-logs-{fileStamp}.csv",
            "json" => $"cortex-logs-{fileStamp}.json",
            "txt" => $"cortex-logs-{fileStamp}.txt",
            "xlsx" => $"cortex-logs-{fileStamp}.xlsx",
            "sheets" => $"cortex-logs-{fileStamp}-google-sheets.xlsx",
            _ => $"cortex-logs-{fileStamp}.csv"
        };

    private static IResult BuildCsvFileResult(IReadOnlyList<AdminRequestLogExportRow> logs, string fileName)
    {
        var csv = BuildCsv(logs);
        var bytes = Encoding.UTF8.GetPreamble()
            .Concat(Encoding.UTF8.GetBytes(csv))
            .ToArray();

        return Results.File(bytes, "text/csv; charset=utf-8", fileName);
    }

    private static IResult BuildJsonFileResult(IReadOnlyList<AdminRequestLogExportRow> logs, string fileName)
    {
        var json = BuildJson(logs);
        var bytes = Encoding.UTF8.GetBytes(json);
        return Results.File(bytes, "application/json", fileName);
    }

    private static IResult BuildTextFileResult(IReadOnlyList<AdminRequestLogExportRow> logs, string fileName)
    {
        var text = BuildText(logs);
        var bytes = Encoding.UTF8.GetBytes(text);
        return Results.File(bytes, "text/plain", fileName);
    }

    private static IResult BuildExcelFileResult(IReadOnlyList<AdminRequestLogExportRow> logs, string fileName)
    {
        var bytes = BuildExcel(logs);
        return Results.File(
            bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
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

    private static string BuildCsv(IReadOnlyList<AdminRequestLogExportRow> rows)
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

    private static string BuildJson(IReadOnlyList<AdminRequestLogExportRow> rows) =>
        JsonConvert.SerializeObject(
            rows,
            new JsonSerializerSettings
            {
                Formatting = Formatting.Indented,
                ContractResolver = new CamelCasePropertyNamesContractResolver()
            });

    private static string BuildText(IReadOnlyList<AdminRequestLogExportRow> rows)
    {
        var builder = new StringBuilder();
        foreach (var row in rows)
        {
            var occurred = row.OccurredUtc.ToString("O", CultureInfo.InvariantCulture);
            builder.Append(occurred);
            builder.Append("  ");
            builder.Append(row.Method);
            builder.Append("  ");
            builder.Append(row.Path);
            builder.Append("  ");
            builder.Append(row.StatusCode.ToString(CultureInfo.InvariantCulture));
            builder.Append("  ");
            builder.Append(row.DurationMs.ToString(CultureInfo.InvariantCulture));
            builder.Append("ms  trace=");
            builder.Append(row.TraceId);
            builder.Append("  authenticated=");
            builder.Append(row.IsAuthenticated ? "true" : "false");
            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static byte[] BuildExcel(IReadOnlyList<AdminRequestLogExportRow> rows)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add("Logs");

        worksheet.Cell(1, 1).Value = "OccurredUtc";
        worksheet.Cell(1, 2).Value = "Method";
        worksheet.Cell(1, 3).Value = "Path";
        worksheet.Cell(1, 4).Value = "StatusCode";
        worksheet.Cell(1, 5).Value = "DurationMs";
        worksheet.Cell(1, 6).Value = "TraceId";
        worksheet.Cell(1, 7).Value = "IsAuthenticated";

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            var excelRow = i + 2;
            worksheet.Cell(excelRow, 1).Value = row.OccurredUtc;
            worksheet.Cell(excelRow, 2).Value = row.Method;
            worksheet.Cell(excelRow, 3).Value = row.Path;
            worksheet.Cell(excelRow, 4).Value = row.StatusCode;
            worksheet.Cell(excelRow, 5).Value = row.DurationMs;
            worksheet.Cell(excelRow, 6).Value = row.TraceId;
            worksheet.Cell(excelRow, 7).Value = row.IsAuthenticated;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private static string EscapeCsv(string? value)
    {
        var normalizedValue = value ?? string.Empty;
        var escapedValue = normalizedValue.Replace("\"", "\"\"");
        return $"\"{escapedValue}\"";
    }
}
