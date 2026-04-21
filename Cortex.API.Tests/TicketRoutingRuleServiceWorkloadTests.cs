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
        Assert.Equal("owner-b", result.RecommendedSynitiOwner);
        Assert.Contains("|000001|0000000011", result.TieBreakKey);
        Assert.Contains("Workload score broke a tie", result.ExplanationText);

        using var document = JsonDocument.Parse(result.ExplanationJson);
        var root = document.RootElement;
        Assert.True(root.GetProperty("workloadTieBreakApplied").GetBoolean());
        Assert.Equal(1, root.GetProperty("selectedWorkloadScore").GetInt32());

        var candidateAssignments = root.GetProperty("candidateAssignments").EnumerateArray().ToList();
        Assert.Equal(2, candidateAssignments.Count);
        Assert.Equal(11, candidateAssignments[0].GetProperty("matchedRuleId").GetInt32());
        Assert.Equal(1, candidateAssignments[0].GetProperty("workloadScore").GetInt32());
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
                new OwnerWorkloadScoreSnapshot("owner-a", 6, 2, 1, 1, 2, 16),
                new OwnerWorkloadScoreSnapshot("owner-b", 0, 0, 0, 0, 0, 0),
            ]);

        await using var context = CreateContext();
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
        Assert.Equal("owner-a", result.RecommendedSynitiOwner);

        using var document = JsonDocument.Parse(result.ExplanationJson);
        Assert.False(document.RootElement.GetProperty("workloadTieBreakApplied").GetBoolean());
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
