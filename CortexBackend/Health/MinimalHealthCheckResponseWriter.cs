using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Cortex.API.Health;

/// <summary>
/// Writes only aggregate status as JSON — no per-check details, exceptions, or connection strings.
/// </summary>
public static class MinimalHealthCheckResponseWriter
{
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json; charset=utf-8";
        return context.Response.WriteAsJsonAsync(new { status = report.Status.ToString() });
    }
}
