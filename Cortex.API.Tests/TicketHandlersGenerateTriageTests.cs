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

public class TicketHandlersGenerateTriageTests
{
    [Fact]
    public async Task GenerateTicketTriage_PersistsAdvisoryFields_WithoutOverwritingCanonicalFields()
    {
        var expectedHints = new[] { "Confirm impacted queue.", "Name the reviewer group." };
        var ticket = new Ticket
        {
            Id = "T-3001",
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
        };

        var repo = new Mock<ITicketRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetTicketByIdAsync(ticket.Id)).ReturnsAsync(ticket);
        repo.Setup(r => r.UpdateTicketAsync(It.IsAny<Ticket>())).ReturnsAsync((Ticket t) => t);
        repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var visibilityService = new Mock<ITicketVisibilityService>(MockBehavior.Strict);
        visibilityService
            .Setup(service => service.GetCurrentVisibilityAsync())
            .ReturnsAsync(new TicketVisibilityContext(
                UserId: 99,
                DisplayName: "Reviewer",
                Email: "reviewer@example.com",
                Scope: TicketVisibilityScope.All));

        var ticketBoardService = new Mock<ITicketBoardService>(MockBehavior.Strict);
        ticketBoardService
            .Setup(service => service.GetByIdAsync(ticket.BoardId))
            .ReturnsAsync(new TicketBoardDefinition
            {
                Id = ticket.BoardId,
                Name = "Ticket",
                IsEnabled = true,
                RequiresStoryPoints = false,
            });

        var userRepository = new Mock<IUserRepository>(MockBehavior.Strict);
        userRepository
            .Setup(repository => repository.GetByIdAsync(ticket.CreatedBy))
            .ReturnsAsync(new User
            {
                Id = ticket.CreatedBy,
                DisplayName = "Requester",
                Email = "requester@example.com",
                Department = "Operations",
                Role = Auth0Roles.User,
            });

        var triageAi = new Mock<ITicketTriageAiService>(MockBehavior.Strict);
        triageAi
            .Setup(service => service.GenerateTriageAsync(
                It.IsAny<TicketTriageInput>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketTriageGenerateResponse
            {
                Summary = "Clarify the approval issue and required workflow outcome.",
                SuggestedPriority = "High",
                PriorityReason = "Approval flow is blocking intake progress.",
                SuggestedStatus = "In Review",
                MissingDetails = expectedHints.ToList(),
                PotentialSlaRisk = "Medium",
                SlaRiskReason = "Unclear scope drives extra clarification cycles before delivery can be bounded.",
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
                    new TicketTriageStatusOption("In Review", "Reviewer triage is underway.", 2),
                ],
                Priorities =
                [
                    new TicketTriagePriorityOption("Low", 48, 24),
                    new TicketTriagePriorityOption("Medium", 24, 12),
                    new TicketTriagePriorityOption("High", 8, 4),
                ],
            });

        var aiSettingsService = new Mock<IAiSettingsService>(MockBehavior.Strict);
        aiSettingsService
            .Setup(service => service.GetAsync())
            .ReturnsAsync(CreateDefaultAiSettings());

        var result = await TicketHandlers.GenerateTicketTriage(
            ticket.Id,
            repo.Object,
            visibilityService.Object,
            ticketBoardService.Object,
            userRepository.Object,
            aiSettingsService.Object,
            triageAi.Object,
            triageVocabulary.Object,
            NullLogger<TicketHandlersLogCategory>.Instance,
            CancellationToken.None);

        var response = await ExecuteGenerateTriageResultAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(response.Body);
        Assert.Equal("Clarify the approval issue and required workflow outcome.", response.Body!.Summary);
        Assert.Equal("High", response.Body.SuggestedPriority);
        Assert.Equal("Approval flow is blocking intake progress.", response.Body.PriorityReason);
        Assert.Equal("In Review", response.Body.SuggestedStatus);
        Assert.Equal("Medium", response.Body.PotentialSlaRisk);
        Assert.Equal(
            "Unclear scope drives extra clarification cycles before delivery can be bounded.",
            response.Body.SlaRiskReason);
        Assert.Equal(expectedHints, response.Body.MissingDetails);

        // Handler returns the AI response as-is; persistence maps vocabulary and updates canonical
        // Priority/Status when suggestions validate (see TicketTriagePersistence.ApplyPersistedResult).
        repo.Verify(
            repository => repository.UpdateTicketAsync(It.Is<Ticket>(updated =>
                updated.Id == ticket.Id &&
                updated.AiTriageSummary == "Clarify the approval issue and required workflow outcome." &&
                updated.AiTriageSuggestedPriority == "High" &&
                updated.AiTriagePriorityReason == "Approval flow is blocking intake progress." &&
                updated.AiTriageSuggestedStatus == "In Review" &&
                updated.Priority == "High" &&
                updated.Status == "In Review" &&
                updated.AiTriagePotentialSlaRisk == "Medium" &&
                updated.AiTriageSlaRiskReason ==
                "Unclear scope drives extra clarification cycles before delivery can be bounded." &&
                DeserializeHints(updated.AiTriageMissingDetailsJson).SequenceEqual(expectedHints))),
            Times.Once);
        repo.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GenerateTicketTriage_InvalidVocabularyFields_ReturnsRawAiResponse_AndPersistsNullAdvisoriesForInvalidFields()
    {
        var expectedHints = new[] { "Confirm impacted queue.", "Name the reviewer group." };
        var ticket = new Ticket
        {
            Id = "T-3002",
            Title = "Repair intake automation",
            Description = "Workflow approval is not advancing.",
            Status = "New",
            ApprovalStatus = ApprovalStatus.PendingApproval,
            Priority = "Medium",
            BoardId = 1,
            CreatedBy = 10,
            CreatedDate = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc),
            LastModifiedBy = 10,
            LastModifiedDate = new DateTime(2026, 4, 19, 12, 0, 0, DateTimeKind.Utc),
        };

        var repo = new Mock<ITicketRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetTicketByIdAsync(ticket.Id)).ReturnsAsync(ticket);
        repo.Setup(r => r.UpdateTicketAsync(It.IsAny<Ticket>())).ReturnsAsync((Ticket t) => t);
        repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var visibilityService = new Mock<ITicketVisibilityService>(MockBehavior.Strict);
        visibilityService
            .Setup(service => service.GetCurrentVisibilityAsync())
            .ReturnsAsync(new TicketVisibilityContext(
                UserId: 99,
                DisplayName: "Reviewer",
                Email: "reviewer@example.com",
                Scope: TicketVisibilityScope.All));

        var ticketBoardService = new Mock<ITicketBoardService>(MockBehavior.Strict);
        ticketBoardService
            .Setup(service => service.GetByIdAsync(ticket.BoardId))
            .ReturnsAsync(new TicketBoardDefinition
            {
                Id = ticket.BoardId,
                Name = "Ticket",
                IsEnabled = true,
                RequiresStoryPoints = false,
            });

        var userRepository = new Mock<IUserRepository>(MockBehavior.Strict);
        userRepository
            .Setup(repository => repository.GetByIdAsync(ticket.CreatedBy))
            .ReturnsAsync(new User
            {
                Id = ticket.CreatedBy,
                DisplayName = "Requester",
                Email = "requester@example.com",
                Department = "Operations",
                Role = Auth0Roles.User,
            });

        var triageAi = new Mock<ITicketTriageAiService>(MockBehavior.Strict);
        triageAi
            .Setup(service => service.GenerateTriageAsync(
                It.IsAny<TicketTriageInput>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketTriageGenerateResponse
            {
                Summary = "Clarify the approval issue and required workflow outcome.",
                SuggestedPriority = "Urgent",
                PriorityReason = " ",
                SuggestedStatus = "Resolved",
                MissingDetails = expectedHints.ToList(),
                PotentialSlaRisk = "Critical",
                SlaRiskReason = "",
                Unavailable = false,
            });

        var triageVocabulary = new Mock<ITicketTriageVocabularyProvider>(MockBehavior.Strict);
        triageVocabulary
            .Setup(v => v.GetAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketTriageVocabularySnapshot
            {
                Statuses =
                [
                    new TicketTriageStatusOption("New", null, 2),
                    new TicketTriageStatusOption("Needs Review", null, 1),
                ],
                Priorities =
                [
                    new TicketTriagePriorityOption("Low", 48, 24),
                    new TicketTriagePriorityOption("High", 8, 4),
                ],
            });

        var aiSettingsService = new Mock<IAiSettingsService>(MockBehavior.Strict);
        aiSettingsService
            .Setup(service => service.GetAsync())
            .ReturnsAsync(CreateDefaultAiSettings());

        var result = await TicketHandlers.GenerateTicketTriage(
            ticket.Id,
            repo.Object,
            visibilityService.Object,
            ticketBoardService.Object,
            userRepository.Object,
            aiSettingsService.Object,
            triageAi.Object,
            triageVocabulary.Object,
            NullLogger<TicketHandlersLogCategory>.Instance,
            CancellationToken.None);

        var response = await ExecuteGenerateTriageResultAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(response.Body);
        // Handler returns the mock AI payload unchanged (no validator/fallback in this path).
        Assert.Equal("Clarify the approval issue and required workflow outcome.", response.Body!.Summary);
        Assert.Equal("Urgent", response.Body.SuggestedPriority);
        Assert.Equal(" ", response.Body.PriorityReason);
        Assert.Equal("Resolved", response.Body.SuggestedStatus);
        Assert.Equal("Critical", response.Body.PotentialSlaRisk);
        Assert.Equal("", response.Body.SlaRiskReason);
        Assert.Equal(expectedHints, response.Body.MissingDetails);

        // Persistence maps only vocabulary-backed fields; invalid priority/status become null advisories.
        repo.Verify(
            repository => repository.UpdateTicketAsync(It.Is<Ticket>(updated =>
                updated.Id == ticket.Id &&
                updated.AiTriageSummary == "Clarify the approval issue and required workflow outcome." &&
                updated.AiTriageSuggestedPriority == null &&
                updated.AiTriagePriorityReason == " " &&
                updated.AiTriageSuggestedStatus == null &&
                updated.Priority == "Medium" &&
                updated.Status == "New" &&
                updated.AiTriagePotentialSlaRisk == "Critical" &&
                updated.AiTriageSlaRiskReason == "" &&
                DeserializeHints(updated.AiTriageMissingDetailsJson).SequenceEqual(expectedHints))),
            Times.Once);
        repo.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    private static async Task<(int StatusCode, TicketTriageGenerateResponse? Body)> ExecuteGenerateTriageResultAsync(
        IResult result)
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

        var body = await JsonSerializer.DeserializeAsync<TicketTriageGenerateResponse>(
            httpContext.Response.Body,
            new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            });

        return (httpContext.Response.StatusCode, body);
    }

    private static IReadOnlyList<string> DeserializeHints(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<List<string>>(json) ?? [];
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
