namespace Cortex.API.Models;

/// <summary>
/// Row shape for admin HTTP request log exports (CSV, JSON, text, Excel).
/// </summary>
public sealed class AdminRequestLogExportRow
{
    public DateTime OccurredUtc { get; init; }

    public string Method { get; init; } = string.Empty;

    public string Path { get; init; } = string.Empty;

    public int StatusCode { get; init; }

    public double DurationMs { get; init; }

    public string TraceId { get; init; } = string.Empty;

    public bool IsAuthenticated { get; init; }

    public static AdminRequestLogExportRow FromEntry(HttpRequestLogEntry entry) =>
        new()
        {
            OccurredUtc = entry.OccurredUtc,
            Method = entry.Method,
            Path = entry.Path,
            StatusCode = entry.StatusCode,
            DurationMs = entry.DurationMs,
            TraceId = entry.TraceId,
            IsAuthenticated = entry.IsAuthenticated
        };
}
