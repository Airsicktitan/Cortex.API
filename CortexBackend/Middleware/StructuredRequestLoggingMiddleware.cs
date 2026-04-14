using System.Diagnostics;
using System.Security.Claims;
using Cortex.API.Data.Repositories;
using Cortex.API.Models;

namespace Cortex.API.Middleware;

/// <summary>
/// Logs each HTTP request once at completion with method, path (no query string), status, duration, trace id, and user id when authenticated.
/// Optionally persists a sanitized row for admin CSV export (no query strings, bodies, or Auth0 subjects).
/// </summary>
public sealed class StructuredRequestLoggingMiddleware
{
    private const int MaxPersistedPathLength = 2048;
    private const int MaxPersistedTraceIdLength = 128;

    private readonly RequestDelegate _next;
    private readonly ILogger<StructuredRequestLoggingMiddleware> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public StructuredRequestLoggingMiddleware(
        RequestDelegate next,
        ILogger<StructuredRequestLoggingMiddleware> logger,
        IServiceScopeFactory scopeFactory)
    {
        _next = next;
        _logger = logger;
        _scopeFactory = scopeFactory;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        if (!ShouldLog(context.Request))
        {
            await _next(context);
            return;
        }

        var start = Stopwatch.GetTimestamp();
        try
        {
            await _next(context);
        }
        finally
        {
            var elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            var traceId = context.TraceIdentifier;
            var method = context.Request.Method;
            var path = context.Request.Path.Value ?? string.Empty;
            var statusCode = context.Response.StatusCode;
            var userSub = context.User.FindFirst("sub")?.Value
                ?? context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            _logger.LogInformation(
                "HTTP {Method} {Path} -> {StatusCode} in {ElapsedMs:F2}ms. TraceId={TraceId} UserSub={UserSub}",
                method,
                path,
                statusCode,
                elapsedMs,
                traceId,
                userSub ?? "(anonymous)");

            if (ShouldPersist(context.Request))
            {
                await TryPersistRequestSummaryAsync(
                    context,
                    method,
                    path,
                    statusCode,
                    elapsedMs,
                    traceId);
            }
        }
    }

    private async Task TryPersistRequestSummaryAsync(
        HttpContext context,
        string method,
        string path,
        int statusCode,
        double elapsedMs,
        string traceId)
    {
        try
        {
            var normalizedPath = path.Length > MaxPersistedPathLength
                ? path[..MaxPersistedPathLength]
                : path;
            var normalizedTraceId = traceId.Length > MaxPersistedTraceIdLength
                ? traceId[..MaxPersistedTraceIdLength]
                : traceId;

            using var scope = _scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IHttpRequestLogRepository>();

            await repository.AddAsync(new HttpRequestLogEntry
            {
                OccurredUtc = DateTime.UtcNow,
                Method = method,
                Path = normalizedPath,
                StatusCode = statusCode,
                DurationMs = elapsedMs,
                TraceId = normalizedTraceId,
                IsAuthenticated = context.User.Identity?.IsAuthenticated == true
            });
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Failed to persist HTTP request log entry.");
        }
    }

    /// <summary>Skips high-churn health checks; still excludes query strings (path only).</summary>
    private static bool ShouldPersist(HttpRequest request)
    {
        if (request.Path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return ShouldLog(request);
    }

    /// <summary>
    /// Limits logs to API and root; skips Swagger UI/static churn and query strings are never included (only <see cref="HttpRequest.Path"/> is logged).
    /// </summary>
    private static bool ShouldLog(HttpRequest request)
    {
        var path = request.Path;
        if (path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return path.StartsWithSegments("/api")
            || path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase)
            || path == "/"
            || path == "";
    }
}

public static class StructuredRequestLoggingMiddlewareExtensions
{
    public static IApplicationBuilder UseStructuredRequestLogging(this IApplicationBuilder app)
    {
        return app.UseMiddleware<StructuredRequestLoggingMiddleware>();
    }
}
