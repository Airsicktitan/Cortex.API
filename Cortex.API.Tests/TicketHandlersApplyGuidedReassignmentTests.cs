using System.Security.Claims;
using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Handlers;
using Cortex.API.Services;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Cortex.API.Tests;

public class TicketHandlersApplyGuidedReassignmentTests
{
    [Fact]
    public async Task ApplyGuidedReassignment_UserWithoutEditRole_ReturnsForbidden()
    {
        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(
                new ClaimsIdentity(
                    [new Claim(ClaimTypes.Role, "User")],
                    authenticationType: "test")),
        };

        var result = await TicketHandlers.ApplyGuidedReassignment(
            "T-9001",
            new ReassignmentApplyRequest { SelectedOwnerId = 1 },
            httpContext,
            Mock.Of<ITicketRepository>(),
            Mock.Of<IUserContextService>(),
            Mock.Of<ITicketVisibilityService>(),
            Mock.Of<ISlaConfigurationService>(),
            Mock.Of<ITicketAuditService>(),
            Mock.Of<ITicketRoutingRuleService>(),
            Mock.Of<IRealtimeEventService>(),
            Mock.Of<IRealtimeAudienceResolver>(),
            Mock.Of<IResponseMappingContextFactory>(),
            Mock.Of<IOperationalRiskService>(),
            Mock.Of<IReassignmentRecommendationService>(),
            Mock.Of<IReassignmentExecutionService>(),
            Mock.Of<IDecisionImpactService>());

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status403Forbidden);
    }
}
