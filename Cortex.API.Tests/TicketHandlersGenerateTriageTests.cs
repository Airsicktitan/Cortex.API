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

public class TicketHandlersGenerateTriageTests
{
    [Fact]
    public async Task GenerateTicketTriage_PersistsAiFieldsAndSaves()
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
                Statuses = [new TicketTriageStatusOption("New", null, 1)],
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
            NullLogger<TicketHandlersLogCategory>.Instance,
            CancellationToken.None);

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status200OK);

        repo.Verify(
            repository => repository.UpdateTicketAsync(It.Is<Ticket>(updated =>
                updated.Id == ticket.Id &&
                updated.AiTriageSummary == "Clarify the approval issue and required workflow outcome." &&
                updated.AiTriageSuggestedPriority == "High" &&
                updated.AiTriagePriorityReason == "Approval flow is blocking intake progress." &&
                updated.AiTriagePotentialSlaRisk == "Medium" &&
                updated.AiTriageSlaRiskReason ==
                "Unclear scope drives extra clarification cycles before delivery can be bounded." &&
                DeserializeHints(updated.AiTriageMissingDetailsJson).SequenceEqual(expectedHints))),
            Times.Once);
        repo.Verify(repository => repository.SaveChangesAsync(), Times.Once);
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
