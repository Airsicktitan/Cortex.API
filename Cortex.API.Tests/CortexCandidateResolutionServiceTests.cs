using Cortex.API.Data;
using Cortex.API.Database;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Cortex.API.Tests;

public class CortexCandidateResolutionServiceTests
{
    [Fact]
    public async Task GetEligibleCandidatesAsync_IncludesZeroTicketFinanceDevelopers()
    {
        await using var context = CreateContext();
        context.Users.AddRange(
            User(1, "Requester", "finance.requester@example.com", Auth0Roles.User, "Finance", eligible: false),
            User(2, "Adam Hooper", "adam@example.com", Auth0Roles.Developer, "Finance", eligible: true),
            User(3, "Jordan Finance", "jordan@example.com", Auth0Roles.Developer, "Finance", eligible: true),
            User(4, "Taylor HR", "taylor@example.com", Auth0Roles.Developer, "HR", eligible: true),
            User(5, "Casey Finance", "casey@example.com", Auth0Roles.User, "Finance", eligible: true),
            User(6, "Inactive Finance", "inactive@example.com", Auth0Roles.Developer, "Finance", eligible: true, active: false));
        await context.SaveChangesAsync();

        var userRepository = new Mock<IUserRepository>(MockBehavior.Strict);
        userRepository
            .Setup(repository => repository.GetByIdAsync(1))
            .ReturnsAsync(context.Users.Single(user => user.Id == 1));

        var routing = new Mock<ITicketRoutingRuleService>(MockBehavior.Strict);
        routing
            .Setup(service => service.EvaluateAsync(
                It.IsAny<RoutingFactors>(),
                "T-FIN-1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RoutingDecisionResult(
                MatchedRuleId: 10,
                OutcomeType: RoutingOutcomeType.RuleMatch,
                ConfidenceLevel: RoutingConfidenceLevel.High,
                NoMatchReason: null,
                RecommendedSynitiOwner: "user:2",
                RecommendedBusinessOwner: null,
                PrecedenceScore: 90,
                TieBreakKey: "test",
                ExplanationJson: """
                    {
                      "candidateAssignments": [
                        { "synitiOwner": "user:2" }
                      ]
                    }
                    """,
                ExplanationText: "test",
                EngineVersion: "test",
                MatchedCriteriaCount: 2));

        var workloadSnapshots = new Mock<IWorkloadSnapshotService>(MockBehavior.Strict);
        workloadSnapshots
            .Setup(service => service.GetSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new WorkloadSnapshot
                {
                    UserId = "user:2",
                    DisplayName = "Adam Hooper",
                    ActiveTicketCount = 8,
                    HighPriorityCount = 3,
                    SlaRiskCount = 2,
                    WorkloadScore = 20,
                    Status = "Overloaded",
                }
            ]);

        var service = new CortexCandidateResolutionService(
            context,
            userRepository.Object,
            routing.Object,
            workloadSnapshots.Object);

        var candidates = await service.GetEligibleCandidatesAsync(new Ticket
        {
            Id = "T-FIN-1",
            Title = "Quarter close mapping issue",
            Priority = "High",
            BoardId = 1,
            CreatedBy = 1,
            SynitiOwner = "user:2",
        });

        var zeroTicketCandidate = Assert.Single(candidates, candidate => candidate.UserId == "user:3");
        Assert.Equal("Jordan Finance", zeroTicketCandidate.DisplayName);
        Assert.True(zeroTicketCandidate.Eligible);
        Assert.Equal(0, zeroTicketCandidate.ActiveTicketCount);
        Assert.Equal(0, zeroTicketCandidate.WorkloadScore);
        Assert.False(zeroTicketCandidate.CurrentlyOverloaded);

        Assert.DoesNotContain(candidates, candidate => candidate.UserId == "user:4");
        Assert.DoesNotContain(candidates, candidate => candidate.UserId == "user:5");
        Assert.DoesNotContain(candidates, candidate => candidate.UserId == "user:6");
        workloadSnapshots.Verify(
            service => service.GetSnapshotsAsync(It.IsAny<CancellationToken>()),
            Times.Once);
        workloadSnapshots.Verify(
            service => service.GetSnapshotAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"cortex-candidates-{Guid.NewGuid():N}")
            .Options;

        return new CortexDbContext(options);
    }

    private static User User(
        int id,
        string displayName,
        string email,
        string role,
        string department,
        bool eligible,
        bool active = true)
    {
        return new User
        {
            Id = id,
            DisplayName = displayName,
            Email = email,
            Role = role,
            Department = department,
            IsActive = active,
            IsSynitiOwnerEligible = eligible,
            IsBusinessOwnerEligible = eligible,
        };
    }
}
