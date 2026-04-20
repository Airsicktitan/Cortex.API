using System.Net;

namespace Cortex.API.Services;

internal static class AiRequestExecution
{
    public static CancellationTokenSource CreateTimeoutScope(
        CancellationToken cancellationToken,
        int timeoutSeconds)
    {
        var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linkedSource.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
        return linkedSource;
    }

    public static int ResolveMaxTokens(int configuredMaxTokens, int featureCeiling)
    {
        return Math.Clamp(configuredMaxTokens, 1, featureCeiling);
    }

    public static bool ShouldRetry(HttpStatusCode? statusCode)
    {
        return statusCode is null
            || statusCode is HttpStatusCode.RequestTimeout
            || statusCode is HttpStatusCode.TooManyRequests
            || statusCode is HttpStatusCode.InternalServerError
            || statusCode is HttpStatusCode.BadGateway
            || statusCode is HttpStatusCode.ServiceUnavailable
            || statusCode is HttpStatusCode.GatewayTimeout;
    }

    public static TimeSpan GetRetryDelay(int attempt)
    {
        var boundedAttempt = Math.Clamp(attempt, 1, 3);
        return TimeSpan.FromMilliseconds(250 * boundedAttempt);
    }
}
