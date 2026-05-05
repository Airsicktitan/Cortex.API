using Cortex.API.Authorization;
using Cortex.API.Configuration;
using Cortex.API.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cortex.API.Tests;

public sealed class TicketEndpointReviewerPolicyTests
{
    [Fact]
    public void CoreApprovalRoutes_Use_ReviewerApprovalAccess()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthorization(options => options.AddCortexPolicies());
        RegisterTicketEndpointsForInspection(builder.Services);
        builder.Services.AddRateLimiter(AiRateLimitPolicies.Configure);

        var app = builder.Build();
        app.MapTicketEndpoints();

        var endpoints = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(ds => ds.Endpoints)
            .OfType<RouteEndpoint>()
            .ToList();

        AssertAuthorizedPolicy(
            endpoints,
            "GetTicketsPendingApproval",
            "/api/tickets/pending-approval",
            CortexAuthorizationExtensions.ReviewerApprovalAccess);

        AssertAuthorizedPolicy(
            endpoints,
            "ApproveTicket",
            "/api/tickets/{id}/approve",
            CortexAuthorizationExtensions.ReviewerApprovalAccess);

        AssertAuthorizedPolicy(
            endpoints,
            "ArchiveTicket",
            "/api/tickets/{id}/archive",
            CortexAuthorizationExtensions.BusinessDataAccess);
    }

    private static void AssertAuthorizedPolicy(
        IReadOnlyCollection<RouteEndpoint> endpoints,
        string endpointName,
        string routePattern,
        string policy)
    {
        var endpoint = endpoints.Single(routeEndpoint =>
            string.Equals(
                routeEndpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName,
                endpointName,
                StringComparison.Ordinal));

        Assert.Equal(routePattern, endpoint.RoutePattern.RawText);

        Assert.Contains(
            endpoint.Metadata.OfType<IAuthorizeData>(),
            authorizeData =>
                string.Equals(
                    authorizeData.Policy,
                    policy,
                    StringComparison.Ordinal));
    }

    /// <summary>Duplicates the AiEndpointGovernanceTests stub graph so endpoints can bind.</summary>
    private static void RegisterTicketEndpointsForInspection(IServiceCollection services)
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

    private static void AddMockSingleton<T>(IServiceCollection services)
        where T : class =>
        services.AddSingleton(_ => Mock.Of<T>());
}
