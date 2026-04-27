using System.Text.Json;
using Cortex.API.Database;
using Cortex.API.Data.Repositories;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Cortex.API.Tests;

public class TicketRoutingRuleServiceWorkloadTests
{
    [Fact]
    public async Task EvaluateAsync_UsesWorkloadScore_ToBreakStaticRoutingTies()
    {
        var repository = new Mock<ITicketRoutingRuleRepository>(MockBehavior.Strict);
        repository
            .Setup(repo => repo.GetAllAsync())
            .ReturnsAsync(
            [
                CreateRule(10, "owner-a", rulePriority: 50, weight: 20),
                CreateRule(11, "owner-b", rulePriority: 50, weight: 20),
            ]);

        var workloadScoringService = new Mock<IOwnerWorkloadScoringService>(MockBehavior.Strict);
        workloadScoringService
            .Setup(service => service.GetScoresAsync(
                It.Is<IEnumerable<string>>(keys => keys.OrderBy(key => key).SequenceEqual(new[] { "owner-a", "owner-b" })),
                null,
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new OwnerWorkloadScoreSnapshot("owner-a", 4, 1, 1, 1, 2, 12),
                new OwnerWorkloadScoreSnapshot("owner-b", 1, 0, 0, 0, 0, 1),
            ]);

        await using var context = CreateContext();
        await SeedEligibleOwnersAsync(context, "owner-a", "owner-b");
        var service = new TicketRoutingRuleService(
            repository.Object,
            context,
            workloadScoringService.Object);

        var result = await service.EvaluateAsync(new RoutingFactors(
            BoardId: "1",
            Priority: "High",
            RequesterDepartment: null,
            RequesterRole: null,
            LegacyDepartment: null,
            LegacyTitle: null));

        Assert.Equal(11, result.MatchedRuleId);
        Assert.Equal("user:2", result.RecommendedSynitiOwner);
        Assert.Contains("Decision engine evaluated slots independently", result.ExplanationText);

        using var document = JsonDocument.Parse(result.ExplanationJson);
        var root = document.RootElement;
        var slot = root
            .GetProperty("slots")
            .GetProperty("synitiOwner");
        Assert.Equal("Moderate match", slot.GetProperty("classification").GetString());
        Assert.True(slot.GetProperty("applied").GetBoolean());

        var candidateAssignments = root.GetProperty("candidateAssignments").EnumerateArray().ToList();
        Assert.Equal(2, candidateAssignments.Count);
        Assert.Contains(candidateAssignments, assignment =>
            assignment.GetProperty("matchedRuleId").GetInt32() == 11
            && assignment.GetProperty("synitiOwner").GetString() == "user:2"
            && assignment.GetProperty("combinedAssignmentWorkloadScore").GetInt32() == 1);
    }

    [Fact]
    public async Task EvaluateAsync_KeepsHigherPriorityRule_AheadOfLowerWorkloadMatch()
    {
        var repository = new Mock<ITicketRoutingRuleRepository>(MockBehavior.Strict);
        repository
            .Setup(repo => repo.GetAllAsync())
            .ReturnsAsync(
            [
                CreateRule(10, "owner-a", rulePriority: 60, weight: 20),
                CreateRule(11, "owner-b", rulePriority: 0, weight: 0),
            ]);

        var workloadScoringService = new Mock<IOwnerWorkloadScoringService>(MockBehavior.Strict);
        workloadScoringService
            .Setup(service => service.GetScoresAsync(
                It.Is<IEnumerable<string>>(keys => keys.OrderBy(key => key).SequenceEqual(new[] { "owner-a", "owner-b" })),
                null,
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new OwnerWorkloadScoreSnapshot("owner-a", 6, 2, 1, 1, 2, 16),
                new OwnerWorkloadScoreSnapshot("owner-b", 0, 0, 0, 0, 0, 0),
            ]);

        await using var context = CreateContext();
        await SeedEligibleOwnersAsync(context, "owner-a", "owner-b");
        var service = new TicketRoutingRuleService(
            repository.Object,
            context,
            workloadScoringService.Object);

        var result = await service.EvaluateAsync(new RoutingFactors(
            BoardId: "1",
            Priority: "High",
            RequesterDepartment: null,
            RequesterRole: null,
            LegacyDepartment: null,
            LegacyTitle: null));

        Assert.Equal(10, result.MatchedRuleId);
        Assert.Equal("user:1", result.RecommendedSynitiOwner);

        using var document = JsonDocument.Parse(result.ExplanationJson);
        var slot = document.RootElement
            .GetProperty("slots")
            .GetProperty("synitiOwner");
        Assert.True(slot.GetProperty("applied").GetBoolean());
        Assert.Equal("Moderate match", slot.GetProperty("classification").GetString());
    }

    [Fact]
    public async Task EvaluateAsync_WeakSignal_RanksButDoesNotAutoAssign()
    {
        var repository = new Mock<ITicketRoutingRuleRepository>(MockBehavior.Strict);
        repository
            .Setup(repo => repo.GetAllAsync())
            .ReturnsAsync(
            [
                new TicketRoutingRule
                {
                    Id = 20,
                    Department = "Operations",
                    SynitiOwner = "owner-a",
                    IsEnabled = true,
                },
            ]);

        var workloadScoringService = new Mock<IOwnerWorkloadScoringService>(MockBehavior.Strict);
        workloadScoringService
            .Setup(service => service.GetScoresAsync(
                It.Is<IEnumerable<string>>(keys => keys.SequenceEqual(new[] { "owner-a" })),
                null,
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([new OwnerWorkloadScoreSnapshot("owner-a", 0, 0, 0, 0, 0, 0)]);

        await using var context = CreateContext();
        await SeedEligibleOwnersAsync(context, "owner-a");
        var service = new TicketRoutingRuleService(
            repository.Object,
            context,
            workloadScoringService.Object);

        var result = await service.EvaluateAsync(new RoutingFactors(
            BoardId: null,
            Priority: null,
            RequesterDepartment: null,
            RequesterRole: null,
            LegacyDepartment: "Operations",
            LegacyTitle: null));

        Assert.Null(result.RecommendedSynitiOwner);

        using var document = JsonDocument.Parse(result.ExplanationJson);
        var slot = document.RootElement
            .GetProperty("slots")
            .GetProperty("synitiOwner");
        Assert.False(slot.GetProperty("applied").GetBoolean());
        Assert.Equal("Limited routing signals", slot.GetProperty("classification").GetString());
        Assert.Equal("owner-a", slot.GetProperty("selectedOwnerDisplayName").GetString());
        Assert.Contains(
            "Limited routing signals",
            slot.GetProperty("appliedReason").GetString());
    }

    [Fact]
    public async Task EvaluateAsync_ExplainsExcludedIneligibleRuleOwners()
    {
        var repository = new Mock<ITicketRoutingRuleRepository>(MockBehavior.Strict);
        repository
            .Setup(repo => repo.GetAllAsync())
            .ReturnsAsync(
            [
                CreateRule(30, "owner-ineligible", rulePriority: 50, weight: 20),
                CreateRule(31, "owner-good", rulePriority: 50, weight: 20),
            ]);

        var workloadScoringService = new Mock<IOwnerWorkloadScoringService>(MockBehavior.Strict);
        workloadScoringService
            .Setup(service => service.GetScoresAsync(
                It.Is<IEnumerable<string>>(keys => keys.OrderBy(key => key).SequenceEqual(new[] { "owner-good", "owner-ineligible" })),
                null,
                false,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new OwnerWorkloadScoreSnapshot("owner-ineligible", 0, 0, 0, 0, 0, 0),
                new OwnerWorkloadScoreSnapshot("owner-good", 0, 0, 0, 0, 0, 0),
            ]);

        await using var context = CreateContext();
        context.Users.AddRange(
            new User
            {
                Email = "owner-ineligible@example.com",
                DisplayName = "owner-ineligible",
                Department = "Syniti",
                IsActive = true,
                IsSynitiOwnerEligible = false,
            },
            new User
            {
                Email = "owner-good@example.com",
                DisplayName = "owner-good",
                Department = "Syniti",
                Role = Auth0Roles.Developer,
                IsActive = true,
                IsSynitiOwnerEligible = true,
            });
        await context.SaveChangesAsync();

        var service = new TicketRoutingRuleService(
            repository.Object,
            context,
            workloadScoringService.Object);

        var result = await service.EvaluateAsync(new RoutingFactors(
            BoardId: "1",
            Priority: "High",
            RequesterDepartment: null,
            RequesterRole: null,
            LegacyDepartment: null,
            LegacyTitle: null));

        Assert.Equal("user:2", result.RecommendedSynitiOwner);

        using var document = JsonDocument.Parse(result.ExplanationJson);
        var skipped = document.RootElement
            .GetProperty("slots")
            .GetProperty("synitiOwner")
            .GetProperty("skippedReasons")
            .EnumerateArray()
            .Single();
        Assert.Equal("InvalidSynitiOwnerRole", skipped.GetProperty("reason").GetString());
        Assert.Equal(
            "Rule target must be an active user in department 'Syniti' and eligible as Syniti owner.",
            skipped.GetProperty("message").GetString());
    }

    [Fact]
    public async Task RecordDecisionAndOverrideAsync_CanonicalizesLegacyOwnerAliases()
    {
        await using var context = CreateContext();
        context.Users.Add(new User
        {
            Id = 42,
            DisplayName = "Adam Hooper",
            Email = "adamcwhooper@yahoo.com",
            Department = "Syniti",
            Role = Auth0Roles.Developer,
            IsActive = true,
            IsSynitiOwnerEligible = true,
            IsBusinessOwnerEligible = true,
        });
        await context.SaveChangesAsync();

        var service = new TicketRoutingRuleService(
            Mock.Of<ITicketRoutingRuleRepository>(),
            context,
            Mock.Of<IOwnerWorkloadScoringService>());
        var decision = new RoutingDecisionResult(
            MatchedRuleId: 1,
            OutcomeType: RoutingOutcomeType.RuleMatch,
            ConfidenceLevel: RoutingConfidenceLevel.High,
            NoMatchReason: null,
            RecommendedSynitiOwner: "Adam Hooper",
            RecommendedBusinessOwner: "adamcwhooper@yahoo.com",
            PrecedenceScore: 1,
            TieBreakKey: "rule:1",
            ExplanationJson: "{}",
            ExplanationText: "test",
            EngineVersion: "test",
            MatchedCriteriaCount: 1);

        var recordedDecision = await service.RecordDecisionAsync("T-42", decision);
        var recordedOverride = await service.RecordOverrideAsync(
            ticketId: "T-42",
            overriddenByUserId: 7,
            previousSynitiOwner: "Adam Hooper",
            previousBusinessOwner: "adamcwhooper@yahoo.com",
            newSynitiOwner: "user:42",
            newBusinessOwner: "Adam Hooper",
            reasonType: RoutingOverrideReasonType.ManualAssignment,
            reasonText: "test");

        Assert.Equal("user:42", recordedDecision.ChosenSynitiOwner);
        Assert.Equal("user:42", recordedDecision.ChosenBusinessOwner);
        Assert.Equal("user:42", recordedOverride.PreviousSynitiOwner);
        Assert.Equal("user:42", recordedOverride.PreviousBusinessOwner);
        Assert.Equal("user:42", recordedOverride.NewSynitiOwner);
        Assert.Equal("user:42", recordedOverride.NewBusinessOwner);
    }

    private static async Task SeedEligibleOwnersAsync(CortexDbContext context, params string[] ownerKeys)
    {
        foreach (var ownerKey in ownerKeys)
        {
            context.Users.Add(new User
            {
                Email = $"{ownerKey}@example.com",
                DisplayName = ownerKey,
                Department = "Syniti",
                Role = Auth0Roles.Developer,
                IsActive = true,
                IsSynitiOwnerEligible = true,
                IsBusinessOwnerEligible = true
            });
        }

        await context.SaveChangesAsync();
    }

    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"routing-workload-{Guid.NewGuid():N}")
            .Options;

        return new CortexDbContext(options);
    }

    private static TicketRoutingRule CreateRule(
        int id,
        string synitiOwner,
        int rulePriority,
        int weight)
    {
        return new TicketRoutingRule
        {
            Id = id,
            BoardId = "1",
            Priority = "High",
            RulePriority = rulePriority,
            Weight = weight,
            SynitiOwner = synitiOwner,
            IsEnabled = true,
        };
    }
}
