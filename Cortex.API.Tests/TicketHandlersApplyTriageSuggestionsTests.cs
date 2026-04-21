using System.Text.Json;
using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Handlers;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TicketHandlersLogCategory = Cortex.API.Handlers.TicketHandlersLogCategory;

namespace Cortex.API.Tests;

public class TicketHandlersApplyTriageSuggestionsTests
{
    [Fact]
    public async Task ApplyTicketTriageSuggestions_ApplysPriorityAndStatus_UpdatesCanonicalFieldsAndPublishesRealtime()
    {
        var ticket = CreatePendingApprovalTicket("T-4101");
        var currentUser = CreateReviewerUser();
        var realtimeMessages = new List<RealtimeEventMessage>();

        var repo = new Mock<ITicketRepository>(MockBehavior.Strict);
        repo.Setup(repository => repository.GetTicketByIdAsync(ticket.Id)).ReturnsAsync(() => ticket);
        repo.Setup(repository => repository.UpdateTicketAsync(It.IsAny<Ticket>())).ReturnsAsync((Ticket t) => t);
        repo.Setup(repository => repository.SaveChangesAsync()).Returns(Task.CompletedTask);

        var userContext = new Mock<IUserContextService>(MockBehavior.Strict);
        userContext.Setup(service => service.GetCurrentUserAsync()).ReturnsAsync(currentUser);

        var visibilityService = CreateVisibilityService();

        var triageVocabulary = new Mock<ITicketTriageVocabularyProvider>(MockBehavior.Strict);
        triageVocabulary
            .Setup(provider => provider.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateVocabulary());

        var ticketAuditService = new Mock<ITicketAuditService>(MockBehavior.Strict);
        ticketAuditService
            .Setup(service => service.RecordTicketUpdatedAsync(
                It.Is<Ticket>(original => original.Priority == "Medium" && original.Status == "New"),
                It.Is<Ticket>(updated => updated.Priority == "High" && updated.Status == "In Review"),
                currentUser,
                "Apply AI triage guidance"))
            .Returns(Task.CompletedTask);

        var slaConfigurationService = new Mock<ISlaConfigurationService>(MockBehavior.Strict);
        slaConfigurationService
            .Setup(service => service.GetPriorityMapAsync())
            .ReturnsAsync(new Dictionary<string, SlaConfiguration>());

        var mappingContextFactory = new Mock<IResponseMappingContextFactory>(MockBehavior.Strict);
        mappingContextFactory
            .Setup(factory => factory.CreateAsync(
                It.Is<IEnumerable<int>>(userIds => userIds.SequenceEqual(new[] { ticket.CreatedBy })),
                null,
                It.Is<IEnumerable<int>>(boardIds => boardIds.SequenceEqual(new[] { ticket.BoardId })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseMappingContext.Empty);
        var operationalRiskService = new Mock<IOperationalRiskService>(MockBehavior.Strict);
        operationalRiskService
            .Setup(service => service.EvaluateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationalRiskResponse());
        var reassignmentRecommendationService =
            new Mock<IReassignmentRecommendationService>(MockBehavior.Strict);
        reassignmentRecommendationService
            .Setup(service => service.EvaluateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReassignmentRecommendationResponse());

        var realtimeEventService = new Mock<IRealtimeEventService>(MockBehavior.Strict);
        realtimeEventService
            .Setup(service => service.PublishAsync(It.IsAny<RealtimeEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<RealtimeEventMessage, CancellationToken>((message, _) => realtimeMessages.Add(message))
            .Returns(ValueTask.CompletedTask);

        var realtimeAudienceResolver = new Mock<IRealtimeAudienceResolver>(MockBehavior.Strict);
        realtimeAudienceResolver
            .Setup(service => service.GetAudienceUserIdsAsync(
                It.Is<Ticket>(updated => updated.Id == ticket.Id),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 42, 77 });

        var result = await TicketHandlers.ApplyTicketTriageSuggestions(
            ticket.Id,
            new TicketTriageApplyRequest
            {
                ApplyPriority = true,
                ApplyStatus = true,
                ChangeReason = "Apply AI triage guidance",
            },
            repo.Object,
            userContext.Object,
            visibilityService.Object,
            triageVocabulary.Object,
            ticketAuditService.Object,
            slaConfigurationService.Object,
            mappingContextFactory.Object,
            operationalRiskService.Object,
            reassignmentRecommendationService.Object,
            realtimeEventService.Object,
            realtimeAudienceResolver.Object,
            NullLogger<TicketHandlersLogCategory>.Instance,
            CancellationToken.None);

        var response = await ExecuteTicketResponseAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(response.Body);
        Assert.Equal("High", response.Body!.Priority);
        Assert.Equal("In Review", response.Body.Status);
        Assert.Equal(ApprovalStatus.PendingApproval, response.Body.ApprovalStatus);
        Assert.NotNull(response.Body.ApprovalTriagePreview);
        Assert.Equal("High", response.Body.ApprovalTriagePreview!.SuggestedPriority);
        Assert.Equal("In Review", response.Body.ApprovalTriagePreview.SuggestedStatus);

        Assert.Equal("High", ticket.Priority);
        Assert.Equal("In Review", ticket.Status);
        Assert.Equal(currentUser.Id, ticket.LastModifiedBy);
        Assert.NotNull(ticket.LastModifiedDate);

        repo.Verify(repository => repository.UpdateTicketAsync(It.IsAny<Ticket>()), Times.Once);
        repo.Verify(repository => repository.SaveChangesAsync(), Times.Once);
        ticketAuditService.VerifyAll();

        Assert.Single(realtimeMessages);
        Assert.Equal("ticket.updated", realtimeMessages[0].EventType);
        Assert.Equal("High", realtimeMessages[0].Ticket!.Priority);
        Assert.Equal("In Review", realtimeMessages[0].Ticket!.Status);
    }

    [Fact]
    public async Task ApplyTicketTriageSuggestions_ApplyPriorityOnly_LeavesStatusUnchanged()
    {
        var ticket = CreatePendingApprovalTicket("T-4102");
        var currentUser = CreateReviewerUser();

        var repo = new Mock<ITicketRepository>(MockBehavior.Strict);
        repo.Setup(repository => repository.GetTicketByIdAsync(ticket.Id)).ReturnsAsync(() => ticket);
        repo.Setup(repository => repository.UpdateTicketAsync(It.IsAny<Ticket>())).ReturnsAsync((Ticket t) => t);
        repo.Setup(repository => repository.SaveChangesAsync()).Returns(Task.CompletedTask);

        var userContext = new Mock<IUserContextService>(MockBehavior.Strict);
        userContext.Setup(service => service.GetCurrentUserAsync()).ReturnsAsync(currentUser);

        var visibilityService = CreateVisibilityService();

        var triageVocabulary = new Mock<ITicketTriageVocabularyProvider>(MockBehavior.Strict);
        triageVocabulary
            .Setup(provider => provider.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateVocabulary());

        var ticketAuditService = new Mock<ITicketAuditService>(MockBehavior.Strict);
        ticketAuditService
            .Setup(service => service.RecordTicketUpdatedAsync(
                It.Is<Ticket>(original => original.Priority == "Medium" && original.Status == "New"),
                It.Is<Ticket>(updated => updated.Priority == "High" && updated.Status == "New"),
                currentUser,
                null))
            .Returns(Task.CompletedTask);

        var slaConfigurationService = new Mock<ISlaConfigurationService>(MockBehavior.Strict);
        slaConfigurationService
            .Setup(service => service.GetPriorityMapAsync())
            .ReturnsAsync(new Dictionary<string, SlaConfiguration>());

        var mappingContextFactory = new Mock<IResponseMappingContextFactory>(MockBehavior.Strict);
        mappingContextFactory
            .Setup(factory => factory.CreateAsync(
                It.Is<IEnumerable<int>>(userIds => userIds.SequenceEqual(new[] { ticket.CreatedBy })),
                null,
                It.Is<IEnumerable<int>>(boardIds => boardIds.SequenceEqual(new[] { ticket.BoardId })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseMappingContext.Empty);
        var operationalRiskService = new Mock<IOperationalRiskService>(MockBehavior.Strict);
        operationalRiskService
            .Setup(service => service.EvaluateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationalRiskResponse());
        var reassignmentRecommendationService =
            new Mock<IReassignmentRecommendationService>(MockBehavior.Strict);
        reassignmentRecommendationService
            .Setup(service => service.EvaluateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReassignmentRecommendationResponse());

        var realtimeEventService = new Mock<IRealtimeEventService>(MockBehavior.Strict);
        realtimeEventService
            .Setup(service => service.PublishAsync(It.IsAny<RealtimeEventMessage>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var realtimeAudienceResolver = new Mock<IRealtimeAudienceResolver>(MockBehavior.Strict);
        realtimeAudienceResolver
            .Setup(service => service.GetAudienceUserIdsAsync(
                It.Is<Ticket>(updated => updated.Id == ticket.Id),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 42, 77 });

        var result = await TicketHandlers.ApplyTicketTriageSuggestions(
            ticket.Id,
            new TicketTriageApplyRequest
            {
                ApplyPriority = true,
                ApplyStatus = false,
            },
            repo.Object,
            userContext.Object,
            visibilityService.Object,
            triageVocabulary.Object,
            ticketAuditService.Object,
            slaConfigurationService.Object,
            mappingContextFactory.Object,
            operationalRiskService.Object,
            reassignmentRecommendationService.Object,
            realtimeEventService.Object,
            realtimeAudienceResolver.Object,
            NullLogger<TicketHandlersLogCategory>.Instance,
            CancellationToken.None);

        var response = await ExecuteTicketResponseAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(response.Body);
        Assert.Equal("High", response.Body!.Priority);
        Assert.Equal("New", response.Body.Status);

        Assert.Equal("High", ticket.Priority);
        Assert.Equal("New", ticket.Status);
        ticketAuditService.VerifyAll();
    }

    [Fact]
    public async Task ApplyTicketTriageSuggestions_ApplyStatusOnly_LeavesPriorityUnchanged()
    {
        var ticket = CreatePendingApprovalTicket("T-4103");
        var currentUser = CreateReviewerUser();

        var repo = new Mock<ITicketRepository>(MockBehavior.Strict);
        repo.Setup(repository => repository.GetTicketByIdAsync(ticket.Id)).ReturnsAsync(() => ticket);
        repo.Setup(repository => repository.UpdateTicketAsync(It.IsAny<Ticket>())).ReturnsAsync((Ticket t) => t);
        repo.Setup(repository => repository.SaveChangesAsync()).Returns(Task.CompletedTask);

        var userContext = new Mock<IUserContextService>(MockBehavior.Strict);
        userContext.Setup(service => service.GetCurrentUserAsync()).ReturnsAsync(currentUser);

        var visibilityService = CreateVisibilityService();

        var triageVocabulary = new Mock<ITicketTriageVocabularyProvider>(MockBehavior.Strict);
        triageVocabulary
            .Setup(provider => provider.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateVocabulary());

        var ticketAuditService = new Mock<ITicketAuditService>(MockBehavior.Strict);
        ticketAuditService
            .Setup(service => service.RecordTicketUpdatedAsync(
                It.Is<Ticket>(original => original.Priority == "Medium" && original.Status == "New"),
                It.Is<Ticket>(updated => updated.Priority == "Medium" && updated.Status == "In Review"),
                currentUser,
                null))
            .Returns(Task.CompletedTask);

        var slaConfigurationService = new Mock<ISlaConfigurationService>(MockBehavior.Strict);
        slaConfigurationService
            .Setup(service => service.GetPriorityMapAsync())
            .ReturnsAsync(new Dictionary<string, SlaConfiguration>());

        var mappingContextFactory = new Mock<IResponseMappingContextFactory>(MockBehavior.Strict);
        mappingContextFactory
            .Setup(factory => factory.CreateAsync(
                It.Is<IEnumerable<int>>(userIds => userIds.SequenceEqual(new[] { ticket.CreatedBy })),
                null,
                It.Is<IEnumerable<int>>(boardIds => boardIds.SequenceEqual(new[] { ticket.BoardId })),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ResponseMappingContext.Empty);
        var operationalRiskService = new Mock<IOperationalRiskService>(MockBehavior.Strict);
        operationalRiskService
            .Setup(service => service.EvaluateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OperationalRiskResponse());
        var reassignmentRecommendationService =
            new Mock<IReassignmentRecommendationService>(MockBehavior.Strict);
        reassignmentRecommendationService
            .Setup(service => service.EvaluateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ReassignmentRecommendationResponse());

        var realtimeEventService = new Mock<IRealtimeEventService>(MockBehavior.Strict);
        realtimeEventService
            .Setup(service => service.PublishAsync(It.IsAny<RealtimeEventMessage>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var realtimeAudienceResolver = new Mock<IRealtimeAudienceResolver>(MockBehavior.Strict);
        realtimeAudienceResolver
            .Setup(service => service.GetAudienceUserIdsAsync(
                It.Is<Ticket>(updated => updated.Id == ticket.Id),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 42, 77 });

        var result = await TicketHandlers.ApplyTicketTriageSuggestions(
            ticket.Id,
            new TicketTriageApplyRequest
            {
                ApplyPriority = false,
                ApplyStatus = true,
            },
            repo.Object,
            userContext.Object,
            visibilityService.Object,
            triageVocabulary.Object,
            ticketAuditService.Object,
            slaConfigurationService.Object,
            mappingContextFactory.Object,
            operationalRiskService.Object,
            reassignmentRecommendationService.Object,
            realtimeEventService.Object,
            realtimeAudienceResolver.Object,
            NullLogger<TicketHandlersLogCategory>.Instance,
            CancellationToken.None);

        var response = await ExecuteTicketResponseAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(response.Body);
        Assert.Equal("Medium", response.Body!.Priority);
        Assert.Equal("In Review", response.Body.Status);

        Assert.Equal("Medium", ticket.Priority);
        Assert.Equal("In Review", ticket.Status);
        ticketAuditService.VerifyAll();
    }

    [Fact]
    public async Task ApplyTicketTriageSuggestions_InvalidCurrentSuggestion_ReturnsConflictWithoutMutatingTicket()
    {
        var ticket = CreatePendingApprovalTicket("T-4104");
        ticket.AiTriageSuggestedStatus = "Resolved";

        var repo = new Mock<ITicketRepository>(MockBehavior.Strict);
        repo.Setup(repository => repository.GetTicketByIdAsync(ticket.Id)).ReturnsAsync(ticket);

        var userContext = new Mock<IUserContextService>(MockBehavior.Strict);
        var visibilityService = CreateVisibilityService();

        var triageVocabulary = new Mock<ITicketTriageVocabularyProvider>(MockBehavior.Strict);
        triageVocabulary
            .Setup(provider => provider.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateVocabulary());

        var ticketAuditService = new Mock<ITicketAuditService>(MockBehavior.Strict);
        var slaConfigurationService = new Mock<ISlaConfigurationService>(MockBehavior.Strict);
        var mappingContextFactory = new Mock<IResponseMappingContextFactory>(MockBehavior.Strict);
        var operationalRiskService = new Mock<IOperationalRiskService>(MockBehavior.Strict);
        var reassignmentRecommendationService =
            new Mock<IReassignmentRecommendationService>(MockBehavior.Strict);
        var realtimeEventService = new Mock<IRealtimeEventService>(MockBehavior.Strict);
        var realtimeAudienceResolver = new Mock<IRealtimeAudienceResolver>(MockBehavior.Strict);

        var result = await TicketHandlers.ApplyTicketTriageSuggestions(
            ticket.Id,
            new TicketTriageApplyRequest
            {
                ApplyPriority = false,
                ApplyStatus = true,
            },
            repo.Object,
            userContext.Object,
            visibilityService.Object,
            triageVocabulary.Object,
            ticketAuditService.Object,
            slaConfigurationService.Object,
            mappingContextFactory.Object,
            operationalRiskService.Object,
            reassignmentRecommendationService.Object,
            realtimeEventService.Object,
            realtimeAudienceResolver.Object,
            NullLogger<TicketHandlersLogCategory>.Instance,
            CancellationToken.None);

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status409Conflict);

        Assert.Equal("Medium", ticket.Priority);
        Assert.Equal("New", ticket.Status);

        repo.Verify(repository => repository.UpdateTicketAsync(It.IsAny<Ticket>()), Times.Never);
        repo.Verify(repository => repository.SaveChangesAsync(), Times.Never);
        ticketAuditService.Verify(
            service => service.RecordTicketUpdatedAsync(
                It.IsAny<Ticket>(),
                It.IsAny<Ticket>(),
                It.IsAny<User>(),
                It.IsAny<string?>()),
            Times.Never);
        realtimeEventService.Verify(
            service => service.PublishAsync(It.IsAny<RealtimeEventMessage>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static async Task<(int StatusCode, TicketResponse? Body)> ExecuteTicketResponseAsync(IResult result)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
        };

        httpContext.Response.Body = new MemoryStream();

        await result.ExecuteAsync(httpContext);

        httpContext.Response.Body.Position = 0;

        var body = await JsonSerializer.DeserializeAsync<TicketResponse>(
            httpContext.Response.Body,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

        return (httpContext.Response.StatusCode, body);
    }

    private static Mock<ITicketVisibilityService> CreateVisibilityService()
    {
        var visibilityService = new Mock<ITicketVisibilityService>(MockBehavior.Strict);
        visibilityService
            .Setup(service => service.GetCurrentVisibilityAsync())
            .ReturnsAsync(new TicketVisibilityContext(
                UserId: 99,
                DisplayName: "Reviewer",
                Email: "reviewer@example.com",
                Scope: TicketVisibilityScope.All));

        return visibilityService;
    }

    private static TicketTriageVocabularySnapshot CreateVocabulary() =>
        new()
        {
            Priorities =
            [
                new TicketTriagePriorityOption("Medium", 24, 12),
                new TicketTriagePriorityOption("High", 8, 4),
            ],
            Statuses =
            [
                new TicketTriageStatusOption("New", null, 1),
                new TicketTriageStatusOption("In Review", null, 2),
            ],
        };

    private static Ticket CreatePendingApprovalTicket(string id) =>
        new()
        {
            Id = id,
            Title = "Review intake request",
            Description = "Need help with approval flow.",
            Status = "New",
            ApprovalStatus = ApprovalStatus.PendingApproval,
            Priority = "Medium",
            BoardId = 1,
            CreatedBy = 10,
            CreatedDate = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc),
            LastModifiedBy = 10,
            LastModifiedDate = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc),
            AiTriageSummary = "Clarify the approval issue and required workflow outcome.",
            AiTriageSuggestedPriority = "High",
            AiTriagePriorityReason = "Approval flow is blocking intake progress.",
            AiTriageSuggestedStatus = "In Review",
            AiTriageMissingDetailsJson = JsonSerializer.Serialize(
                new[] { "Confirm impacted queue.", "Name the reviewer group." }),
            AiTriagePotentialSlaRisk = "Medium",
            AiTriageSlaRiskReason =
                "Unclear scope drives extra clarification cycles before delivery can be bounded.",
        };

    private static User CreateReviewerUser() =>
        new()
        {
            Id = 99,
            Email = "reviewer@example.com",
            DisplayName = "Reviewer",
            Department = "Operations",
            Role = Auth0Roles.BusinessManager,
        };
}
