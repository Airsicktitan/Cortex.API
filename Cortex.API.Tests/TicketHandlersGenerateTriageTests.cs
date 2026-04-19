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

        var result = await TicketHandlers.GenerateTicketTriage(
            ticket.Id,
            repo.Object,
            visibilityService.Object,
            ticketBoardService.Object,
            userRepository.Object,
            triageAi.Object,
            triageVocabulary.Object,
            new TicketTriageResponseValidator(),
            new TicketTriageFallbackPolicy(),
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

        repo.Verify(
            repository => repository.UpdateTicketAsync(It.Is<Ticket>(updated =>
                updated.Id == ticket.Id &&
                updated.AiTriageSummary == "Clarify the approval issue and required workflow outcome." &&
                updated.AiTriageSuggestedPriority == "High" &&
                updated.AiTriagePriorityReason == "Approval flow is blocking intake progress." &&
                updated.AiTriageSuggestedStatus == "In Review" &&
                updated.Priority == "Medium" &&
                updated.Status == "New" &&
                updated.AiTriagePotentialSlaRisk == "Medium" &&
                updated.AiTriageSlaRiskReason ==
                "Unclear scope drives extra clarification cycles before delivery can be bounded." &&
                DeserializeHints(updated.AiTriageMissingDetailsJson).SequenceEqual(expectedHints))),
            Times.Once);
        repo.Verify(repository => repository.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task GenerateTicketTriage_InvalidAiFields_PersistsFallbackAdvisoryValues_WithoutOverwritingCanonicalFields()
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

        var result = await TicketHandlers.GenerateTicketTriage(
            ticket.Id,
            repo.Object,
            visibilityService.Object,
            ticketBoardService.Object,
            userRepository.Object,
            triageAi.Object,
            triageVocabulary.Object,
            new TicketTriageResponseValidator(),
            new TicketTriageFallbackPolicy(),
            NullLogger<TicketHandlersLogCategory>.Instance,
            CancellationToken.None);

        var response = await ExecuteGenerateTriageResultAsync(result);

        Assert.Equal(StatusCodes.Status200OK, response.StatusCode);
        Assert.NotNull(response.Body);
        Assert.Equal("Clarify the approval issue and required workflow outcome.", response.Body!.Summary);
        Assert.Equal("Low", response.Body.SuggestedPriority);
        Assert.Equal(
            "Default configured priority applied pending reviewer confirmation.",
            response.Body.PriorityReason);
        Assert.Equal("Needs Review", response.Body.SuggestedStatus);
        Assert.Equal("Medium", response.Body.PotentialSlaRisk);
        Assert.Equal(
            "Clarification is still needed before delivery pressure can be assessed more precisely.",
            response.Body.SlaRiskReason);
        Assert.Equal(expectedHints, response.Body.MissingDetails);

        repo.Verify(
            repository => repository.UpdateTicketAsync(It.Is<Ticket>(updated =>
                updated.Id == ticket.Id &&
                updated.AiTriageSummary == "Clarify the approval issue and required workflow outcome." &&
                updated.AiTriageSuggestedPriority == "Low" &&
                updated.AiTriagePriorityReason == "Default configured priority applied pending reviewer confirmation." &&
                updated.AiTriageSuggestedStatus == "Needs Review" &&
                updated.Priority == "Medium" &&
                updated.Status == "New" &&
                updated.AiTriagePotentialSlaRisk == "Medium" &&
                updated.AiTriageSlaRiskReason ==
                "Clarification is still needed before delivery pressure can be assessed more precisely." &&
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
}
