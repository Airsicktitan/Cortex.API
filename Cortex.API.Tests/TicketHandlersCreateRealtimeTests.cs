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

public class TicketHandlersCreateRealtimeTests
{
    [Fact]
    public async Task CreateTicket_PublishesSingleTicketCreatedRealtimeEvent()
    {
        const string ticketId = "3001";

        var request = new CreateTicketRequest
        {
            Title = "New routed ticket",
            Description = "Created from test",
            Priority = "High",
        };

        var currentUser = new User
        {
            Id = 42,
            Email = "creator@test.com",
            DisplayName = "Creator",
            Department = "Operations",
            Role = Auth0Roles.User,
        };
        var board = new TicketBoardDefinition
        {
            Id = 1,
            Name = "Ticket",
            RequiresStoryPoints = false,
            IsEnabled = true,
        };
        var routingDecision = new RoutingDecisionResult(
            MatchedRuleId: 11,
            OutcomeType: RoutingOutcomeType.RuleMatch,
            ConfidenceLevel: RoutingConfidenceLevel.High,
            NoMatchReason: null,
            RecommendedSynitiOwner: "Syniti Owner",
            RecommendedBusinessOwner: "Business Owner",
            PrecedenceScore: 100,
            TieBreakKey: "rule:11",
            ExplanationJson: "{}",
            ExplanationText: "Matched routing rule",
            EngineVersion: "routing-engine-v1",
            MatchedCriteriaCount: 3);

        Ticket? createdTicket = null;
        var ticketRepository = new Mock<ITicketRepository>(MockBehavior.Strict);
        ticketRepository
            .Setup(repository => repository.GetNextTicketIdAsync())
            .ReturnsAsync(ticketId);
        ticketRepository
            .Setup(repository => repository.CreateTicketAsync(It.IsAny<Ticket>()))
            .Callback<Ticket>(ticket =>
            {
                createdTicket = ticket;
                createdTicket.RowVersion = [1, 2, 3, 4, 5, 6, 7, 8];
            })
            .ReturnsAsync((Ticket ticket) => ticket);
        ticketRepository
            .Setup(repository => repository.SaveChangesAsync())
            .Returns(Task.CompletedTask);
        ticketRepository
            .Setup(repository => repository.GetTicketByIdAsync(ticketId))
            .ReturnsAsync(() => createdTicket);

        var userContext = new Mock<IUserContextService>(MockBehavior.Strict);
        userContext
            .Setup(service => service.GetCurrentUserAsync())
            .ReturnsAsync(currentUser);

        var slaConfigurationService = new Mock<ISlaConfigurationService>(MockBehavior.Strict);
        slaConfigurationService
            .Setup(service => service.GetPriorityMapAsync())
            .ReturnsAsync(new Dictionary<string, SlaConfiguration>());

        var ticketBoardService = new Mock<ITicketBoardService>(MockBehavior.Strict);
        ticketBoardService
            .Setup(service => service.GetDefaultCreateBoardAsync())
            .ReturnsAsync(board);

        var ticketRoutingRuleService = new Mock<ITicketRoutingRuleService>(MockBehavior.Strict);
        ticketRoutingRuleService
            .Setup(service => service.EvaluateAsync(It.IsAny<RoutingFactors>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(routingDecision);
        ticketRoutingRuleService
            .Setup(service => service.RecordDecisionAsync(ticketId, routingDecision, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketRoutingDecision
            {
                TicketId = ticketId,
                MatchedRuleId = routingDecision.MatchedRuleId,
                ChosenSynitiOwner = routingDecision.RecommendedSynitiOwner,
                ChosenBusinessOwner = routingDecision.RecommendedBusinessOwner,
                OutcomeType = routingDecision.OutcomeType,
                ConfidenceLevel = routingDecision.ConfidenceLevel,
                PrecedenceScore = routingDecision.PrecedenceScore,
                TieBreakKey = routingDecision.TieBreakKey,
                ExplanationJson = routingDecision.ExplanationJson,
                ExplanationText = routingDecision.ExplanationText,
                EngineVersion = routingDecision.EngineVersion,
            });

        var ticketAuditService = new Mock<ITicketAuditService>(MockBehavior.Strict);
        ticketAuditService
            .Setup(service => service.RecordTicketCreatedAsync(It.IsAny<Ticket>(), currentUser, request.ChangeReason))
            .Returns(Task.CompletedTask);

        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        notificationService
            .Setup(service => service.CreateAssignmentNotificationsForNewTicketAsync(It.IsAny<Ticket>(), currentUser))
            .ReturnsAsync(0);

        var realtimeMessages = new List<RealtimeEventMessage>();
        var realtimeEventService = new Mock<IRealtimeEventService>(MockBehavior.Strict);
        realtimeEventService
            .Setup(service => service.PublishAsync(It.IsAny<RealtimeEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<RealtimeEventMessage, CancellationToken>((message, _) => realtimeMessages.Add(message))
            .Returns(ValueTask.CompletedTask);

        var realtimeAudienceResolver = new Mock<IRealtimeAudienceResolver>(MockBehavior.Strict);
        realtimeAudienceResolver
            .Setup(service => service.GetAudienceUserIdsAsync(
                It.Is<Ticket>(ticket => ticket.Id == ticketId),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 42, 77 });

        var mappingContextFactory = new Mock<IResponseMappingContextFactory>(MockBehavior.Strict);
        mappingContextFactory
            .Setup(factory => factory.CreateAsync(
                It.Is<IEnumerable<int>>(userIds => userIds.SequenceEqual(new[] { 42 })),
                null,
                It.Is<IEnumerable<int>>(boardIds => boardIds.SequenceEqual(new[] { 1 })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseMappingContext.Empty);

        var result = await TicketHandlers.CreateTicket(
            request,
            ticketRepository.Object,
            userContext.Object,
            Mock.Of<IUserRepository>(),
            slaConfigurationService.Object,
            Mock.Of<ITicketStatusService>(),
            ticketBoardService.Object,
            ticketRoutingRuleService.Object,
            ticketAuditService.Object,
            notificationService.Object,
            realtimeEventService.Object,
            realtimeAudienceResolver.Object,
            mappingContextFactory.Object,
            NullLogger<TicketHandlersLogCategory>.Instance);

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status201Created);

        Assert.Single(realtimeMessages);
        var realtimeMessage = realtimeMessages[0];
        Assert.Equal("ticket.created", realtimeMessage.EventType);
        Assert.Equal(ticketId, realtimeMessage.TicketId);
        Assert.Equal(ticketId, realtimeMessage.EntityId);
        Assert.NotNull(realtimeMessage.Ticket);
        Assert.Equal("Syniti Owner", realtimeMessage.Ticket!.SynitiOwner);
        Assert.Equal("Business Owner", realtimeMessage.Ticket.BusinessOwner);
    }
}
