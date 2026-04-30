using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cortex.API.Services;

public sealed class TicketCreationApplicationService(
    ITicketRepository repo,
    IServiceScopeFactory serviceScopeFactory,
    IUserContextService userContext,
    IUserRepository userRepository,
    ISlaConfigurationService slaConfigurationService,
    ITicketBoardService ticketBoardService,
    ITicketRoutingRuleService ticketRoutingRuleService,
    ITicketAuditService ticketAuditService,
    IOperationalRiskService operationalRiskService,
    IReassignmentRecommendationService reassignmentRecommendationService,
    IRealtimeEventService realtimeEventService,
    IRealtimeAudienceResolver realtimeAudienceResolver,
    IResponseMappingContextFactory mappingContextFactory,
    IWorkflowMetricsService workflowMetrics,
    ILogger<TicketHandlersLogCategory> logger,
    ITicketOutcomeService? ticketOutcomeService = null,
    ICortexAutonomyService? cortexAutonomyService = null) : ITicketCreationApplicationService
{
    public Task<TicketResponse> CreateTicketAsync(
        CreateTicketRequest request,
        CancellationToken cancellationToken = default) =>
        TicketHandlers.CreateTicketCoreAsync(
            request,
            repo,
            serviceScopeFactory,
            userContext,
            userRepository,
            slaConfigurationService,
            ticketBoardService,
            ticketRoutingRuleService,
            ticketAuditService,
            operationalRiskService,
            reassignmentRecommendationService,
            realtimeEventService,
            realtimeAudienceResolver,
            mappingContextFactory,
            workflowMetrics,
            logger,
            ticketOutcomeService,
            cortexAutonomyService,
            cancellationToken);
}
