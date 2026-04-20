using System.Security.Claims;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cortex.API.Configuration;

public static class AiRateLimitPolicies
{
    public const string StandardPolicyName = "ai-standard";
    public const string VisionPolicyName = "ai-vision";

    private const int StandardRequestsPerMinute = 20;
    private const int VisionRequestsPerMinute = 6;

    public static void Configure(RateLimiterOptions options)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = OnRejectedAsync;

        options.AddPolicy(
            StandardPolicyName,
            httpContext => CreateTokenBucketPartition(httpContext, StandardRequestsPerMinute));
        options.AddPolicy(
            VisionPolicyName,
            httpContext => CreateTokenBucketPartition(httpContext, VisionRequestsPerMinute));
    }

    private static RateLimitPartition<string> CreateTokenBucketPartition(
        HttpContext httpContext,
        int tokenLimit)
    {
        return RateLimitPartition.GetTokenBucketLimiter(
            partitionKey: GetPartitionKey(httpContext),
            factory: _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = tokenLimit,
                TokensPerPeriod = tokenLimit,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                AutoReplenishment = true,
            });
    }

    private static string GetPartitionKey(HttpContext httpContext)
    {
        var userId = GetAuthenticatedUserId(httpContext.User);
        if (!string.IsNullOrWhiteSpace(userId))
        {
            return userId;
        }

        return httpContext.Connection.RemoteIpAddress is not null
            ? $"ip:{httpContext.Connection.RemoteIpAddress}"
            : "anonymous";
    }

    private static string? GetAuthenticatedUserId(ClaimsPrincipal user) =>
        user.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? user.FindFirst("sub")?.Value;

    private static async ValueTask OnRejectedAsync(OnRejectedContext context, CancellationToken token)
    {
        var httpContext = context.HttpContext;

        int? retryAfterSeconds = null;
        if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
        {
            retryAfterSeconds = (int)Math.Ceiling(retryAfter.TotalSeconds);
            httpContext.Response.Headers.RetryAfter = retryAfterSeconds.Value.ToString();
        }

        var policyName = httpContext.GetEndpoint()?
            .Metadata
            .GetMetadata<EnableRateLimitingAttribute>()?
            .PolicyName;

        if (policyName is not null &&
            policyName.StartsWith("ai-", StringComparison.Ordinal))
        {
            var userId = GetAuthenticatedUserId(httpContext.User);
            var clientIp = httpContext.Connection.RemoteIpAddress?.ToString();
            var partitionKey = !string.IsNullOrWhiteSpace(userId)
                ? userId
                : clientIp is not null
                    ? $"ip:{clientIp}"
                    : "anonymous";

            var logger = httpContext.RequestServices
                .GetService<ILoggerFactory>()?
                .CreateLogger(typeof(AiRateLimitPolicies));

            logger?.LogWarning(
                "AI rate limit rejected request. Policy={PolicyName} Method={Method} Path={Path} UserId={UserId} PartitionKey={PartitionKey} ClientIp={ClientIp} RetryAfterSeconds={RetryAfterSeconds}",
                policyName,
                httpContext.Request.Method,
                httpContext.Request.Path.Value,
                userId,
                partitionKey,
                clientIp,
                retryAfterSeconds);
        }

        httpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        httpContext.Response.ContentType = "text/plain";
        await httpContext.Response.WriteAsync(
            "Rate limit exceeded. Try again shortly.",
            token);
    }
}
