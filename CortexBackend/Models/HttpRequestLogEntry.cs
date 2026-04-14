namespace Cortex.API.Models;

/// <summary>
/// Sanitized HTTP request summary persisted for admin export. No query strings, bodies, or identity provider subjects.
/// </summary>
public class HttpRequestLogEntry
{
    public long Id { get; set; }

    public DateTime OccurredUtc { get; set; }

    public string Method { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public int StatusCode { get; set; }

    public double DurationMs { get; set; }

    public string TraceId { get; set; } = string.Empty;

    /// <summary>True when the request had an authenticated principal after the pipeline ran.</summary>
    public bool IsAuthenticated { get; set; }
}
