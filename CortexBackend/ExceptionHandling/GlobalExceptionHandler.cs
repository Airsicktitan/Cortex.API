using System.Security.Claims;
using Microsoft.AspNetCore.Diagnostics;

namespace Cortex.API.ExceptionHandling;

/// <summary>
/// Logs unhandled exceptions with request correlation context and returns a generic JSON body.
/// Does not log Authorization headers, query strings, or request bodies.
/// </summary>
public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var traceId = httpContext.TraceIdentifier;
        var path = httpContext.Request.Path.Value ?? string.Empty;
        var method = httpContext.Request.Method;
        var userSub = httpContext.User.FindFirst("sub")?.Value
            ?? httpContext.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        using (_logger.BeginScope(new Dictionary<string, object?>
        {
            ["TraceId"] = traceId,
            ["HttpMethod"] = method,
            ["RequestPath"] = path,
            ["UserSub"] = userSub ?? "(anonymous)",
        }))
        {
            // Pass the exception instance so the full stack and inner exceptions are recorded.
            _logger.LogError(exception, "Unhandled API exception");
        }

        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/json; charset=utf-8";

        await httpContext.Response.WriteAsJsonAsync(
            new
            {
                message = "An unexpected error occurred. Please try again later.",
                traceId,
            },
            cancellationToken: cancellationToken);

        return true;
    }
}
