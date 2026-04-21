using System.Text.Json;
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

        var aiSettingsService = new Mock<IAiSettingsService>(MockBehavior.Strict);
        aiSettingsService
            .Setup(service => service.GetAsync())
            .ReturnsAsync(CreateDefaultAiSettings());

        var ticketBoardService = new Mock<ITicketBoardService>(MockBehavior.Strict);
        ticketBoardService
            .Setup(service => service.GetDefaultCreateBoardAsync())
            .ReturnsAsync(board);
        ticketBoardService
            .Setup(service => service.GetByIdAsync(It.Is<int>(id => id == board.Id)))
            .ReturnsAsync(board);

        var userRepository = new Mock<IUserRepository>(MockBehavior.Strict);
        userRepository
            .Setup(repository => repository.GetByIdAsync(currentUser.Id))
            .ReturnsAsync(currentUser);

        var triageAi = new Mock<ITicketTriageAiService>(MockBehavior.Strict);
        triageAi
            .Setup(service => service.GenerateTriageAsync(
                It.IsAny<TicketTriageInput>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketTriageGenerateResponse { Unavailable = true });

        var triageVocabulary = new Mock<ITicketTriageVocabularyProvider>(MockBehavior.Strict);
        triageVocabulary
            .Setup(v => v.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketTriageVocabularySnapshot
            {
                Statuses = [new TicketTriageStatusOption("New", null, 1)],
                Priorities =
                [
                    new TicketTriagePriorityOption("Low", 48, 24),
                    new TicketTriagePriorityOption("Medium", 24, 12),
                    new TicketTriagePriorityOption("High", 8, 4),
                ],
            });

        var ticketRoutingRuleService = new Mock<ITicketRoutingRuleService>(MockBehavior.Strict);
        ticketRoutingRuleService
            .Setup(service => service.EvaluateAsync(It.IsAny<RoutingFactors>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(routingDecision);

        var ticketAuditService = new Mock<ITicketAuditService>(MockBehavior.Strict);
        ticketAuditService
            .Setup(service => service.RecordTicketCreatedAsync(It.IsAny<Ticket>(), currentUser, request.ChangeReason))
            .Returns(Task.CompletedTask);

        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var operationalRiskService = new Mock<IOperationalRiskService>(MockBehavior.Strict);
        operationalRiskService
            .Setup(service => service.EvaluateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationalRiskResponse());
        var reassignmentRecommendationService =
            new Mock<IReassignmentRecommendationService>(MockBehavior.Strict);
        reassignmentRecommendationService
            .Setup(service => service.EvaluateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReassignmentRecommendationResponse());

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
            userRepository.Object,
            aiSettingsService.Object,
            slaConfigurationService.Object,
            Mock.Of<ITicketStatusService>(),
            ticketBoardService.Object,
            ticketRoutingRuleService.Object,
            triageAi.Object,
            triageVocabulary.Object,
            ticketAuditService.Object,
            operationalRiskService.Object,
            reassignmentRecommendationService.Object,
            notificationService.Object,
            realtimeEventService.Object,
            realtimeAudienceResolver.Object,
            mappingContextFactory.Object,
            Mock.Of<IWorkflowMetricsService>(),
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
        Assert.Equal(ApprovalStatus.PendingApproval, realtimeMessage.Ticket.ApprovalStatus);
    }

    [Fact]
    public async Task CreateTicket_PersistsAiAdvisoryFields_AndAppliesValidatedTriageToCanonicalPriorityAndStatus()
    {
        const string ticketId = "3002";

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
            .Setup(repository => repository.UpdateTicketAsync(It.IsAny<Ticket>()))
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

        var aiSettingsService = new Mock<IAiSettingsService>(MockBehavior.Strict);
        aiSettingsService
            .Setup(service => service.GetAsync())
            .ReturnsAsync(CreateDefaultAiSettings());

        var ticketBoardService = new Mock<ITicketBoardService>(MockBehavior.Strict);
        ticketBoardService
            .Setup(service => service.GetDefaultCreateBoardAsync())
            .ReturnsAsync(board);
        ticketBoardService
            .Setup(service => service.GetByIdAsync(It.Is<int>(id => id == board.Id)))
            .ReturnsAsync(board);

        var userRepository = new Mock<IUserRepository>(MockBehavior.Strict);
        userRepository
            .Setup(repository => repository.GetByIdAsync(currentUser.Id))
            .ReturnsAsync(currentUser);

        var triageAi = new Mock<ITicketTriageAiService>(MockBehavior.Strict);
        triageAi
            .Setup(service => service.GenerateTriageAsync(
                It.IsAny<TicketTriageInput>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketTriageGenerateResponse
            {
                Summary = "Clarify the reviewer handoff and approval outcome.",
                SuggestedPriority = "Low",
                PriorityReason = "This request is bounded and can be reviewed in normal queue order.",
                SuggestedStatus = "In Review",
                MissingDetails = ["Confirm the target approver.", "Confirm the acceptance criteria."],
                PotentialSlaRisk = "Low",
                SlaRiskReason = "The request is narrow and the next clarification steps are clear.",
                Unavailable = false,
            });

        var triageVocabulary = new Mock<ITicketTriageVocabularyProvider>(MockBehavior.Strict);
        triageVocabulary
            .Setup(v => v.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketTriageVocabularySnapshot
            {
                Statuses =
                [
                    new TicketTriageStatusOption("New", null, 1),
                    new TicketTriageStatusOption("In Review", null, 2),
                ],
                Priorities =
                [
                    new TicketTriagePriorityOption("Low", 48, 24),
                    new TicketTriagePriorityOption("High", 8, 4),
                ],
            });

        var ticketRoutingRuleService = new Mock<ITicketRoutingRuleService>(MockBehavior.Strict);
        ticketRoutingRuleService
            .Setup(service => service.EvaluateAsync(It.IsAny<RoutingFactors>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(routingDecision);

        var ticketAuditService = new Mock<ITicketAuditService>(MockBehavior.Strict);
        ticketAuditService
            .Setup(service => service.RecordTicketCreatedAsync(It.IsAny<Ticket>(), currentUser, request.ChangeReason))
            .Returns(Task.CompletedTask);

        var notificationService = new Mock<INotificationService>(MockBehavior.Strict);
        var operationalRiskService = new Mock<IOperationalRiskService>(MockBehavior.Strict);
        operationalRiskService
            .Setup(service => service.EvaluateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationalRiskResponse());
        var reassignmentRecommendationService =
            new Mock<IReassignmentRecommendationService>(MockBehavior.Strict);
        reassignmentRecommendationService
            .Setup(service => service.EvaluateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReassignmentRecommendationResponse());

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
            userRepository.Object,
            aiSettingsService.Object,
            slaConfigurationService.Object,
            Mock.Of<ITicketStatusService>(),
            ticketBoardService.Object,
            ticketRoutingRuleService.Object,
            triageAi.Object,
            triageVocabulary.Object,
            ticketAuditService.Object,
            operationalRiskService.Object,
            reassignmentRecommendationService.Object,
            notificationService.Object,
            realtimeEventService.Object,
            realtimeAudienceResolver.Object,
            mappingContextFactory.Object,
            Mock.Of<IWorkflowMetricsService>(),
            NullLogger<TicketHandlersLogCategory>.Instance);

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status201Created);

        Assert.NotNull(createdTicket);
        // Create path runs triage persistence: validated AI suggestions update canonical Priority/Status
        // when they match vocabulary (TicketTriagePersistence.ApplyAiSuggestedPriorityToTicket / Status).
        Assert.Equal("Low", createdTicket!.Priority);
        Assert.Equal("In Review", createdTicket.Status);
        Assert.Equal("Clarify the reviewer handoff and approval outcome.", createdTicket.AiTriageSummary);
        Assert.Equal("Low", createdTicket.AiTriageSuggestedPriority);
        Assert.Equal(
            "This request is bounded and can be reviewed in normal queue order.",
            createdTicket.AiTriagePriorityReason);
        Assert.Equal("In Review", createdTicket.AiTriageSuggestedStatus);
        Assert.Equal("Low", createdTicket.AiTriagePotentialSlaRisk);
        Assert.Equal(
            "The request is narrow and the next clarification steps are clear.",
            createdTicket.AiTriageSlaRiskReason);
        Assert.Equal(
            ["Confirm the target approver.", "Confirm the acceptance criteria."],
            JsonSerializer.Deserialize<List<string>>(createdTicket.AiTriageMissingDetailsJson!) ?? []);

        Assert.Single(realtimeMessages);
        Assert.Equal("Low", realtimeMessages[0].Ticket!.Priority);
        Assert.Equal("In Review", realtimeMessages[0].Ticket!.Status);

        ticketRepository.Verify(repository => repository.UpdateTicketAsync(It.IsAny<Ticket>()), Times.Once);
        ticketRepository.Verify(repository => repository.SaveChangesAsync(), Times.Exactly(2));
    }

    private static AiSettingsConfiguration CreateDefaultAiSettings() =>
        new()
        {
            IsIntakeAssistEnabled = true,
            IsTriageEnabled = true,
            IsScreenshotInsightEnabled = true,
            IsSuggestedUpdatesEnabled = false,
            IsPriorityRecommendationEnabled = true,
            IsStatusRecommendationEnabled = true,
            DefaultTextModel = "gpt-4o-mini",
            DefaultVisionModel = "gpt-4o-mini",
            Temperature = 0.2,
            MaxTokens = 1800,
            TimeoutSeconds = 120,
            RetryCount = 0,
            AdvisoryOnlyMode = false,
            AllowStatusRecommendation = true,
            AllowPriorityRecommendation = true,
            SuggestionOnlyMode = false,
            ConfidenceThreshold = 0.7,
            MaxScreenshotAttachmentCount = 5,
        };
}
