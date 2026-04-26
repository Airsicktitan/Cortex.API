using System.Diagnostics;
using System.Net;
using System.Security.Claims;
using Cortex.API.Authorization;
using Cortex.API.Configuration;
using Cortex.API.Extensions;
using Cortex.API.Data;
using Cortex.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cortex.API.Tests;

public class AiEndpointGovernanceTests
{
    [Fact]
    public async Task VisionPolicy_Returns429WithRetryAfter_WhenBurstLimitIsExceeded()
    {
        using var services = BuildRateLimitedServices();
        var pipeline = BuildRateLimitedPipeline(
            services,
            "/ai/screenshot-insight",
            AiRateLimitPolicies.VisionPolicyName);

        var statusCodes = new List<int>();
        string? retryAfter = null;
        string? rejectionBody = null;

        for (var attempt = 0; attempt < 7; attempt++)
        {
            var context = new DefaultHttpContext
            {
                RequestServices = services,
                User = CreateAuthenticatedUser("pilot-user"),
            };

            context.Connection.RemoteIpAddress = IPAddress.Parse("203.0.113.10");
            context.Request.Method = HttpMethods.Post;
            context.Request.Path = "/ai/screenshot-insight";
            context.Response.Body = new MemoryStream();

            await pipeline(context);

            statusCodes.Add(context.Response.StatusCode);
            if (context.Response.StatusCode == StatusCodes.Status429TooManyRequests)
            {
                retryAfter = context.Response.Headers.RetryAfter.ToString();
                rejectionBody = await ReadBodyAsync(context);
            }
        }

        Assert.Equal(
            [
                StatusCodes.Status200OK,
                StatusCodes.Status200OK,
                StatusCodes.Status200OK,
                StatusCodes.Status200OK,
                StatusCodes.Status200OK,
                StatusCodes.Status200OK,
                StatusCodes.Status429TooManyRequests,
            ],
            statusCodes);
        Assert.False(string.IsNullOrWhiteSpace(retryAfter));
        Assert.True(int.TryParse(retryAfter, out var retryAfterSeconds));
        Assert.True(retryAfterSeconds > 0);
        Assert.Equal("Rate limit exceeded. Try again shortly.", rejectionBody);
    }

    [Fact]
    public void KnownAiEndpoints_AreAuthorized_AndUseExpectedPolicies()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization(options => options.AddCortexPolicies());
        RegisterTicketEndpointServices(builder.Services);

        var app = builder.Build();
        app.MapTicketEndpoints();
        app.MapTicketAttachmentEndpoints();
        app.MapAiSettingsEndpoints();
        app.MapAiEndpoints();
        app.MapRepeatIssueEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        AssertGovernedEndpoint(
            endpoints,
            endpointName: "GenerateTicketTriage",
            routePattern: "/api/tickets/{id}/triage",
            expectedPolicy: AiRateLimitPolicies.StandardPolicyName);
        AssertGovernedEndpoint(
            endpoints,
            endpointName: "ApplyTicketTriageSuggestions",
            routePattern: "/api/tickets/{id}/triage/apply",
            expectedPolicy: AiRateLimitPolicies.StandardPolicyName);
        AssertGovernedEndpoint(
            endpoints,
            endpointName: "ImproveTicketIntake",
            routePattern: "/api/tickets/intake-assist",
            expectedPolicy: AiRateLimitPolicies.StandardPolicyName);
        AssertGovernedEndpoint(
            endpoints,
            endpointName: "AnalyzeScreenshotAttachments",
            routePattern: "/api/tickets/{ticketId}/attachments/screenshot-insight",
            expectedPolicy: AiRateLimitPolicies.VisionPolicyName);
        AssertGovernedEndpoint(
            endpoints,
            endpointName: "GenerateRepeatIssueAiReview",
            routePattern: "/api/metrics/repeat-issues/{groupKey}/ai-review",
            expectedPolicy: AiRateLimitPolicies.StandardPolicyName);
        AssertGovernedEndpoint(
            endpoints,
            endpointName: "PostCortexAiAssess",
            routePattern: "/api/ai/assess",
            expectedPolicy: AiRateLimitPolicies.StandardPolicyName);

        Assert.DoesNotContain(
            endpoints,
            endpoint => string.Equals(
                endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName,
                "ai-policy",
                StringComparison.Ordinal));

        AssertAdminEndpoint(
            endpoints,
            endpointName: "GetAiSettings",
            routePattern: "/api/settings/ai/");
        AssertAdminEndpoint(
            endpoints,
            endpointName: "UpdateAiSettings",
            routePattern: "/api/settings/ai/");
    }

    /// <summary>
    /// Regression guard for policy-name drift: every endpoint that calls
    /// <c>RequireRateLimiting(...)</c> must reference a policy name that is
    /// actually registered via <see cref="AiRateLimitPolicies"/>. If a new
    /// policy is added to <see cref="AiRateLimitPolicies"/>, include it in
    /// <see cref="RegisteredPolicyNames"/> below so this guard keeps covering it.
    /// </summary>
    [Fact]
    public void AllRateLimitedEndpoints_ReferenceRegisteredPolicies()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization(options => options.AddCortexPolicies());
        builder.Services.AddRateLimiter(AiRateLimitPolicies.Configure);
        RegisterTicketEndpointServices(builder.Services);

        var app = builder.Build();
        app.MapTicketEndpoints();
        app.MapTicketAttachmentEndpoints();
        app.MapAiSettingsEndpoints();
        app.MapAiEndpoints();
        app.MapRepeatIssueEndpoints();

        var rateLimitedEndpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(dataSource => dataSource.Endpoints)
            .OfType<RouteEndpoint>()
            .Select(endpoint => new
            {
                Endpoint = endpoint,
                PolicyName = endpoint.Metadata
                    .GetMetadata<EnableRateLimitingAttribute>()?
                    .PolicyName,
            })
            .Where(entry => !string.IsNullOrEmpty(entry.PolicyName))
            .ToList();

        Assert.NotEmpty(rateLimitedEndpoints);

        foreach (var entry in rateLimitedEndpoints)
        {
            Assert.Contains(
                entry.PolicyName!,
                RegisteredPolicyNames);
        }
    }

    private static readonly IReadOnlySet<string> RegisteredPolicyNames =
        new HashSet<string>(StringComparer.Ordinal)
        {
            AiRateLimitPolicies.StandardPolicyName,
            AiRateLimitPolicies.VisionPolicyName,
        };

    private static ServiceProvider BuildRateLimitedServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRouting();
        services.AddSingleton(_ => new DiagnosticListener("Cortex.API.Tests"));
        services.AddSingleton<DiagnosticSource>(serviceProvider =>
            serviceProvider.GetRequiredService<DiagnosticListener>());
        services.AddRateLimiter(AiRateLimitPolicies.Configure);

        return services.BuildServiceProvider();
    }

    private static void RegisterTicketEndpointServices(IServiceCollection services)
    {
        AddMockSingleton<ITicketRepository>(services);
        AddMockSingleton<ITicketAttachmentRepository>(services);
        AddMockSingleton<IUserRepository>(services);
        AddMockSingleton<ICommentRepository>(services);
        AddMockSingleton<IUserContextService>(services);
        AddMockSingleton<IAiSettingsService>(services);
        AddMockSingleton<ISlaConfigurationService>(services);
        AddMockSingleton<ITicketVisibilityService>(services);
        AddMockSingleton<IOwnerWorkloadPreviewService>(services);
        AddMockSingleton<ITicketAuditService>(services);
        AddMockSingleton<IResponseMappingContextFactory>(services);
        AddMockSingleton<IRealtimeAudienceResolver>(services);
        AddMockSingleton<IWorkflowMetricsService>(services);
        AddMockSingleton<ITicketTriageAiService>(services);
        AddMockSingleton<ITicketTriageVocabularyProvider>(services);
        AddMockSingleton<ITicketIntakeAssistAiService>(services);
        AddMockSingleton<IScreenshotInsightAiService>(services);
        AddMockSingleton<IRealtimeEventService>(services);
        AddMockSingleton<ITicketBoardService>(services);
        AddMockSingleton<ITicketRoutingRuleService>(services);
        AddMockSingleton<ITicketStatusService>(services);
        AddMockSingleton<INotificationService>(services);
        AddMockSingleton<IRepeatIssueAnalyticsService>(services);
        AddMockSingleton<IRepeatIssueAiReviewService>(services);
        AddMockSingleton<ICortexDecisionService>(services);
        AddMockSingleton<ICortexAiAssessmentService>(services);
        AddMockSingleton<ICortexCandidateResolutionService>(services);
        AddMockSingleton<ICortexEmbeddingService>(services);
        AddMockSingleton<ICortexMemoryFeedbackService>(services);
    }

    private static RequestDelegate BuildRateLimitedPipeline(
        IServiceProvider services,
        string path,
        string policyName)
    {
        var appBuilder = new ApplicationBuilder(services);
        appBuilder.UseRouting();
        appBuilder.UseRateLimiter();
        appBuilder.UseEndpoints(endpoints =>
        {
            endpoints.MapPost(
                    path,
                    async context =>
                    {
                        context.Response.StatusCode = StatusCodes.Status200OK;
                        await context.Response.WriteAsync("ok");
                    })
                .RequireRateLimiting(policyName);
        });

        return appBuilder.Build();
    }

    private static ClaimsPrincipal CreateAuthenticatedUser(string userId) =>
        new(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, userId)],
            authenticationType: "Test"));

    private static void AddMockSingleton<T>(IServiceCollection services)
        where T : class =>
        services.AddSingleton(_ => Mock.Of<T>());

    private static async Task<string> ReadBodyAsync(HttpContext context)
    {
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, leaveOpen: true);
        var body = await reader.ReadToEndAsync();
        context.Response.Body.Position = 0;
        return body;
    }

    private static void AssertGovernedEndpoint(
        IReadOnlyCollection<RouteEndpoint> endpoints,
        string endpointName,
        string routePattern,
        string expectedPolicy)
    {
        var endpoint = endpoints.Single(routeEndpoint =>
            string.Equals(
                routeEndpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName,
                endpointName,
                StringComparison.Ordinal));

        Assert.Equal(routePattern, endpoint.RoutePattern.RawText);
        Assert.True(endpoint.Metadata.OfType<IAuthorizeData>().Any());
        Assert.Equal(
            expectedPolicy,
            endpoint.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName);
    }

    private static void AssertAdminEndpoint(
        IReadOnlyCollection<RouteEndpoint> endpoints,
        string endpointName,
        string routePattern)
    {
        var endpoint = endpoints.Single(routeEndpoint =>
            string.Equals(
                routeEndpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName,
                endpointName,
                StringComparison.Ordinal));

        Assert.Equal(routePattern, endpoint.RoutePattern.RawText);
        Assert.Contains(
            endpoint.Metadata.OfType<IAuthorizeData>(),
            authorizeData => string.Equals(
                authorizeData.Policy,
                CortexAuthorizationExtensions.AdminOnly,
                StringComparison.Ordinal));
    }
}
