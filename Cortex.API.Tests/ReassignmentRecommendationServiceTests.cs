using System.Text.Json;
using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Moq;

namespace Cortex.API.Tests;

public class ReassignmentRecommendationServiceTests
{
    [Fact]
    public async Task EvaluateAsync_OverloadedOwnerWithBetterCandidates_ReturnsSuggestions()
    {
        var service = CreateService(
            risk: new OperationalRiskResponse
            {
                RiskLevel = "high",
                IsOwnerOverloaded = true,
            },
            routingResult: BuildRoutingResult(
                ("owner-a", null, 27),
                ("owner-b", null, 12),
                ("owner-c", null, 14)),
            ownerScores:
            [
                new OwnerWorkloadScoreSnapshot("owner-a", 10, 4, 3, 2, 5, 27),
                new OwnerWorkloadScoreSnapshot("owner-b", 4, 1, 0, 0, 0, 12),
                new OwnerWorkloadScoreSnapshot("owner-c", 5, 1, 0, 0, 0, 14),
            ],
            users: CreateUsers("owner-a", "owner-b", "owner-c"));

        var ticket = CreateTicket("T-1", "owner-a", null);
        var recommendation = await service.EvaluateAsync(ticket);

        Assert.True(recommendation.ShouldSuggestReassignment);
        Assert.Equal("Current owner has elevated workload and lower-risk eligible alternatives exist.", recommendation.Reason);
        Assert.NotNull(recommendation.CurrentOwner);
        Assert.Equal(27, recommendation.CurrentOwner!.WorkloadScore);
        Assert.Equal(2, recommendation.SuggestedTargets.Count);
        Assert.Equal("owner-b", recommendation.SuggestedTargets[0].OwnerKey);
        Assert.Equal(12, recommendation.SuggestedTargets[0].WorkloadScore);
    }

    [Fact]
    public async Task EvaluateAsync_AlternativesNotMeaningfullyBetter_DoesNotSuggest()
    {
        var service = CreateService(
            risk: new OperationalRiskResponse
            {
                RiskLevel = "high",
                IsOwnerOverloaded = true,
            },
            routingResult: BuildRoutingResult(
                ("owner-a", null, 18),
                ("owner-b", null, 16)),
            ownerScores:
            [
                new OwnerWorkloadScoreSnapshot("owner-a", 5, 2, 1, 0, 1, 18),
                new OwnerWorkloadScoreSnapshot("owner-b", 4, 1, 1, 0, 1, 16),
            ],
            users: CreateUsers("owner-a", "owner-b"));

        var ticket = CreateTicket("T-2", "owner-a", null);
        var recommendation = await service.EvaluateAsync(ticket);

        Assert.False(recommendation.ShouldSuggestReassignment);
        Assert.Empty(recommendation.SuggestedTargets);
        Assert.Equal(
            "No eligible owners are meaningfully lower risk than the current assignment.",
            recommendation.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_NoEligibleAlternatives_DoesNotSuggest()
    {
        var service = CreateService(
            risk: new OperationalRiskResponse
            {
                RiskLevel = "critical",
                IsOwnerOverloaded = true,
            },
            routingResult: BuildRoutingResult(("owner-a", null, 27)),
            ownerScores: [new OwnerWorkloadScoreSnapshot("owner-a", 8, 3, 2, 1, 3, 27)],
            users: CreateUsers("owner-a"));

        var ticket = CreateTicket("T-3", "owner-a", null);
        var recommendation = await service.EvaluateAsync(ticket);

        Assert.False(recommendation.ShouldSuggestReassignment);
        Assert.Empty(recommendation.SuggestedTargets);
    }

    [Fact]
    public async Task EvaluateAsync_CurrentOwnerMissing_DoesNotSuggest()
    {
        var service = CreateService(
            risk: new OperationalRiskResponse
            {
                RiskLevel = "critical",
                IsOwnerOverloaded = true,
            },
            routingResult: BuildRoutingResult(("owner-b", null, 10)),
            ownerScores: [],
            users: CreateUsers("owner-b"));

        var ticket = CreateTicket("T-4", null, null);
        var recommendation = await service.EvaluateAsync(ticket);

        Assert.False(recommendation.ShouldSuggestReassignment);
        Assert.Equal("Current owner is missing.", recommendation.Reason);
    }

    [Fact]
    public async Task EvaluateAsync_RanksLowerWorkloadFirst_AndCapsToTopThree()
    {
        var service = CreateService(
            risk: new OperationalRiskResponse
            {
                RiskLevel = "high",
                IsOwnerOverloaded = true,
            },
            routingResult: BuildRoutingResult(
                ("owner-a", null, 30),
                ("owner-d", null, 17),
                ("owner-b", null, 12),
                ("owner-c", null, 14),
                ("owner-e", null, 13)),
            ownerScores:
            [
                new OwnerWorkloadScoreSnapshot("owner-a", 10, 4, 3, 2, 5, 30),
                new OwnerWorkloadScoreSnapshot("owner-d", 6, 2, 1, 0, 1, 17),
                new OwnerWorkloadScoreSnapshot("owner-b", 4, 1, 0, 0, 0, 12),
                new OwnerWorkloadScoreSnapshot("owner-c", 5, 1, 0, 0, 0, 14),
                new OwnerWorkloadScoreSnapshot("owner-e", 4, 1, 0, 0, 0, 13),
            ],
            users: CreateUsers("owner-a", "owner-b", "owner-c", "owner-d", "owner-e"));

        var ticket = CreateTicket("T-5", "owner-a", null);
        var recommendation = await service.EvaluateAsync(ticket);

        Assert.True(recommendation.ShouldSuggestReassignment);
        Assert.Equal(3, recommendation.SuggestedTargets.Count);
        Assert.True(
            recommendation.SuggestedTargets.Select(target => target.OwnerKey).SequenceEqual(
                ["owner-b", "owner-e", "owner-c"]));
    }

    [Fact]
    public async Task EvaluateAsync_PreservesDeterministicOrderingForTies()
    {
        var service = CreateService(
            risk: new OperationalRiskResponse
            {
                RiskLevel = "high",
                IsOwnerOverloaded = true,
            },
            routingResult: BuildRoutingResult(
                ("owner-a", null, 30),
                ("adam", null, 10),
                ("sarah", null, 10)),
            ownerScores: [new OwnerWorkloadScoreSnapshot("owner-a", 10, 4, 3, 2, 5, 30)],
            users: new[]
            {
                new User { Id = 1, DisplayName = "Owner A", Email = "owner-a@example.com" },
                new User { Id = 2, DisplayName = "Sarah", Email = "sarah@example.com" },
                new User { Id = 3, DisplayName = "Adam", Email = "adam@example.com" },
            });

        var ticket = CreateTicket("T-6", "owner-a", null);
        var recommendation = await service.EvaluateAsync(ticket);

        Assert.True(
            recommendation.SuggestedTargets.Select(target => target.OwnerKey).SequenceEqual(
                ["adam", "sarah"]));
    }

    private static ReassignmentRecommendationService CreateService(
        OperationalRiskResponse risk,
        RoutingDecisionResult routingResult,
        IReadOnlyList<OwnerWorkloadScoreSnapshot> ownerScores,
        IReadOnlyList<User> users)
    {
        var routingService = new Mock<ITicketRoutingRuleService>(MockBehavior.Strict);
        routingService
            .Setup(service => service.EvaluateAsync(
                It.IsAny<RoutingFactors>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(routingResult);

        var workloadService = new Mock<IOwnerWorkloadScoringService>(MockBehavior.Strict);
        workloadService
            .Setup(service => service.GetScoresAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownerScores);

        var riskService = new Mock<IOperationalRiskService>(MockBehavior.Strict);
        riskService
            .Setup(service => service.EvaluateBatchAsync(
                It.IsAny<IEnumerable<Ticket>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Ticket> tickets, CancellationToken _) =>
                tickets.ToDictionary(ticket => ticket.Id, _ => risk, StringComparer.Ordinal));

        var userRepository = new Mock<IUserRepository>(MockBehavior.Strict);
        userRepository
            .Setup(repository => repository.GetAllUsersAsync())
            .ReturnsAsync(users);
        userRepository
            .Setup(repository => repository.GetByIdAsync(It.IsAny<int>()))
            .ReturnsAsync(new User
            {
                Id = 42,
                DisplayName = "Requester",
                Email = "requester@example.com",
                Department = "Operations",
                Role = "User",
            });

        return new ReassignmentRecommendationService(
            routingService.Object,
            workloadService.Object,
            riskService.Object,
            userRepository.Object);
    }

    private static RoutingDecisionResult BuildRoutingResult(
        params (string? SynitiOwner, string? BusinessOwner, int WorkloadScore)[] candidates)
    {
        var explanation = new
        {
            candidateAssignments = candidates.Select((candidate, index) => new
            {
                matchedRuleId = index + 1,
                synitiOwner = candidate.SynitiOwner,
                businessOwner = candidate.BusinessOwner,
                workloadScore = candidate.WorkloadScore,
            })
        };

        return new RoutingDecisionResult(
            MatchedRuleId: 1,
            OutcomeType: RoutingOutcomeType.RuleMatch,
            ConfidenceLevel: RoutingConfidenceLevel.High,
            NoMatchReason: null,
            RecommendedSynitiOwner: candidates.FirstOrDefault().SynitiOwner,
            RecommendedBusinessOwner: candidates.FirstOrDefault().BusinessOwner,
            PrecedenceScore: 1,
            TieBreakKey: "1",
            ExplanationJson: JsonSerializer.Serialize(explanation),
            ExplanationText: "test",
            EngineVersion: "test",
            MatchedCriteriaCount: 1);
    }

    private static List<User> CreateUsers(params string[] ownerKeys)
    {
        var users = new List<User>();
        for (var i = 0; i < ownerKeys.Length; i++)
        {
            users.Add(new User
            {
                Id = i + 1,
                DisplayName = ownerKeys[i],
                Email = $"{ownerKeys[i]}@example.com",
            });
        }

        return users;
    }

    private static Ticket CreateTicket(
        string id,
        string? synitiOwner,
        string? businessOwner)
    {
        return new Ticket
        {
            Id = id,
            Title = $"Ticket {id}",
            Description = "Reassignment test",
            Status = "New",
            ApprovalStatus = ApprovalStatus.Approved,
            Priority = "High",
            BoardId = 1,
            SynitiOwner = synitiOwner,
            BusinessOwner = businessOwner,
            CreatedBy = 42,
            CreatedDate = DateTime.UtcNow,
            LastModifiedBy = 42,
            LastModifiedDate = DateTime.UtcNow,
        };
    }
}
