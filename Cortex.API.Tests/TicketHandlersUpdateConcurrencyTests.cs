using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Handlers;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TicketHandlersLogCategory = Cortex.API.Handlers.TicketHandlersLogCategory;

namespace Cortex.API.Tests;

public class TicketHandlersUpdateConcurrencyTests
{
    [Fact]
    public async Task UpdateTicket_MismatchedConcurrencyToken_ReturnsConflict()
    {
        const string ticketId = "2002";
        var existingRowVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var staleClientToken = Convert.ToBase64String(new byte[] { 9, 9, 9, 9, 9, 9, 9, 9 });

        var ticket = new Ticket
        {
            Id = ticketId,
            Title = "T",
            Description = "D",
            Status = "New",
            Priority = "Medium",
            BoardId = 1,
            CreatedBy = 1,
            CreatedDate = DateTime.UtcNow,
            LastModifiedBy = 1,
            RowVersion = existingRowVersion,
        };

        var repo = new Mock<ITicketRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetTicketByIdAsync(ticketId)).ReturnsAsync(ticket);

        var userContext = new Mock<IUserContextService>(MockBehavior.Strict);
        userContext
            .Setup(u => u.GetCurrentUserAsync())
            .ReturnsAsync(
                new User
                {
                    Id = 99,
                    Email = "actor@test.com",
                    DisplayName = "Actor",
                });

        var request = new UpdateTicketRequest
        {
            ConcurrencyToken = staleClientToken,
        };

        var httpContext = new DefaultHttpContext();

        var result = await TicketHandlers.UpdateTicket(
            ticketId,
            request,
            httpContext,
            repo.Object,
            userContext.Object,
            Mock.Of<IUserRepository>(),
            Mock.Of<ISlaConfigurationService>(),
            Mock.Of<ITicketStatusService>(),
            Mock.Of<ITicketBoardService>(),
            Mock.Of<ITicketRoutingRuleService>(),
            Mock.Of<ITicketTriageAiService>(),
            Mock.Of<ITicketTriageVocabularyProvider>(),
            Mock.Of<ITicketAuditService>(),
            Mock.Of<INotificationService>(),
            Mock.Of<IRealtimeEventService>(),
            Mock.Of<IRealtimeAudienceResolver>(),
            Mock.Of<IResponseMappingContextFactory>(),
            Mock.Of<IWorkflowMetricsService>(),
            NullLogger<TicketHandlersLogCategory>.Instance);

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status409Conflict);

        repo.Verify(r => r.UpdateTicketAsync(It.IsAny<Ticket>()), Times.Never);
        repo.Verify(r => r.SaveChangesAsync(), Times.Never);
    }
}
