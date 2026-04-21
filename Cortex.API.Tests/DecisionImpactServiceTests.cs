using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Cortex.API.Tests;

public class DecisionImpactServiceTests
{
    [Fact]
    public async Task EvaluateAsync_RiskImproved_ReturnsRiskReductionSummary()
    {
        await using var context = CreateContext();
        var appliedAt = DateTime.UtcNow.AddMinutes(-10);
        context.TicketRoutingOverrides.Add(CreateSnapshot(
            previousRiskLevel: "high",
            previousWorkload: 27,
            previousPressureLevel: "high",
            appliedAt));
        await context.SaveChangesAsync();

        var service = CreateService(
            context,
            currentRiskLevel: "moderate",
            currentWorkloadScore: 12);

        var impact = await service.EvaluateAsync(CreateTicket("owner-b"));

        Assert.NotNull(impact);
        Assert.True(impact!.HasImpact);
        Assert.True(impact.RiskImproved);
        Assert.Equal("high", impact.PreviousRiskLevel);
        Assert.Equal("moderate", impact.CurrentRiskLevel);
        Assert.Equal("Risk reduced from High to Moderate", impact.Summary);
        Assert.Equal(appliedAt, impact.AppliedAtUtc);
    }

    [Fact]
    public async Task EvaluateAsync_NoImprovement_ReturnsNeutralSummary()
    {
        await using var context = CreateContext();
        context.TicketRoutingOverrides.Add(CreateSnapshot(
            previousRiskLevel: "low",
            previousWorkload: 4,
            previousPressureLevel: "low",
            DateTime.UtcNow));
        await context.SaveChangesAsync();

        var service = CreateService(
            context,
            currentRiskLevel: "high",
            currentWorkloadScore: 12);

        var impact = await service.EvaluateAsync(CreateTicket("owner-b"));

        Assert.NotNull(impact);
        Assert.False(impact!.RiskImproved);
        Assert.False(impact.WorkloadImproved);
        Assert.False(impact.PressureImproved);
        Assert.Equal("No significant improvement detected", impact.Summary);
    }

    [Fact]
    public async Task EvaluateAsync_WorkloadImproved_ReturnsWorkloadSummary()
    {
        await using var context = CreateContext();
        context.TicketRoutingOverrides.Add(CreateSnapshot(
            previousRiskLevel: "low",
            previousWorkload: 10,
            previousPressureLevel: "low",
            DateTime.UtcNow));
        await context.SaveChangesAsync();

        var service = CreateService(
            context,
            currentRiskLevel: "low",
            currentWorkloadScore: 5);

        var impact = await service.EvaluateAsync(CreateTicket("owner-b"));

        Assert.NotNull(impact);
        Assert.True(impact!.WorkloadImproved);
        Assert.Equal(10, impact.PreviousOwnerWorkload);
        Assert.Equal(5, impact.CurrentOwnerWorkload);
        Assert.Equal("Reassigned to lower workload owner", impact.Summary);
    }

    [Fact]
    public async Task EvaluateAsync_PressureImproved_ReturnsPressureSummary()
    {
        await using var context = CreateContext();
        context.TicketRoutingOverrides.Add(CreateSnapshot(
            previousRiskLevel: "low",
            previousWorkload: 25,
            previousPressureLevel: "high",
            DateTime.UtcNow));
        await context.SaveChangesAsync();

        var service = CreateService(
            context,
            currentRiskLevel: "low",
            currentWorkloadScore: 10);

        var impact = await service.EvaluateAsync(CreateTicket("owner-b"));

        Assert.NotNull(impact);
        Assert.True(impact!.PressureImproved);
        Assert.Equal("high", impact.PreviousPressureLevel);
        Assert.Equal("low", impact.CurrentPressureLevel);
        Assert.Equal("Owner pressure improved from High to Low", impact.Summary);
    }

    [Fact]
    public async Task EvaluateAsync_MissingSnapshot_ReturnsNull()
    {
        await using var context = CreateContext();
        var riskService = new Mock<IOperationalRiskService>(MockBehavior.Strict);
        var workloadService = new Mock<IOwnerWorkloadScoringService>(MockBehavior.Strict);
        var service = new DecisionImpactService(
            context,
            riskService.Object,
            workloadService.Object);

        var impact = await service.EvaluateAsync(CreateTicket("owner-b"));

        Assert.Null(impact);
        riskService.VerifyNoOtherCalls();
        workloadService.VerifyNoOtherCalls();
    }

    private static DecisionImpactService CreateService(
        CortexDbContext context,
        string currentRiskLevel,
        int currentWorkloadScore)
    {
        var riskService = new Mock<IOperationalRiskService>(MockBehavior.Strict);
        riskService
            .Setup(service => service.EvaluateBatchAsync(
                It.IsAny<IEnumerable<Ticket>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Ticket> tickets, CancellationToken _) =>
                tickets.ToDictionary(
                    ticket => ticket.Id,
                    _ => new OperationalRiskResponse { RiskLevel = currentRiskLevel },
                    StringComparer.Ordinal));

        var workloadService = new Mock<IOwnerWorkloadScoringService>(MockBehavior.Strict);
        workloadService
            .Setup(service => service.GetScoresAsync(
                It.Is<IEnumerable<string>>(keys => keys.SequenceEqual(new[] { "owner-b" })),
                null,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new OwnerWorkloadScoreSnapshot("owner-b", 1, 0, 0, 0, 0, currentWorkloadScore),
            ]);

        return new DecisionImpactService(
            context,
            riskService.Object,
            workloadService.Object);
    }

    private static TicketRoutingOverride CreateSnapshot(
        string previousRiskLevel,
        int previousWorkload,
        string previousPressureLevel,
        DateTime appliedAtUtc) =>
        new()
        {
            TicketId = "T-900",
            OverriddenByUserId = 99,
            PreviousSynitiOwner = "owner-a",
            NewSynitiOwner = "owner-b",
            OverrideReasonType = RoutingOverrideReasonType.WorkloadAdjustment,
            CreatedDateUtc = appliedAtUtc,
            DecisionImpactPreviousOwnerId = 10,
            DecisionImpactAssignmentField = "synitiOwner",
            DecisionImpactPreviousOwnerWorkload = previousWorkload,
            DecisionImpactPreviousPressureLevel = previousPressureLevel,
            DecisionImpactPreviousRiskLevel = previousRiskLevel,
            DecisionImpactPreviousSlaStatus = "At Risk",
            DecisionImpactAppliedAtUtc = appliedAtUtc,
            DecisionImpactSource = "cortex_recommendation_review",
        };

    private static Ticket CreateTicket(string synitiOwner) =>
        new()
        {
            Id = "T-900",
            Title = "Decision impact ticket",
            Description = "Decision impact test",
            Status = "New",
            ApprovalStatus = ApprovalStatus.Approved,
            Priority = "High",
            BoardId = 1,
            SynitiOwner = synitiOwner,
            BusinessOwner = null,
            CreatedBy = 1,
            CreatedDate = DateTime.UtcNow,
            LastModifiedBy = 1,
            LastModifiedDate = DateTime.UtcNow,
        };

    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"decision-impact-{Guid.NewGuid():N}")
            .Options;

        return new CortexDbContext(options);
    }
}
