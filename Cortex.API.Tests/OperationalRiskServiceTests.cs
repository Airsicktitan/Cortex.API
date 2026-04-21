using Cortex.API.Models;
using Cortex.API.Services;
using Moq;

namespace Cortex.API.Tests;

public class OperationalRiskServiceTests
{
    [Fact]
    public async Task EvaluateAsync_HighRiskTicket_ReturnsHighRiskAndEscalationRecommendation()
    {
        var ownerScoringService = new Mock<IOwnerWorkloadScoringService>(MockBehavior.Strict);
        ownerScoringService
            .Setup(service => service.GetScoresAsync(
                It.Is<IEnumerable<string>>(keys =>
                    keys.OrderBy(k => k).SequenceEqual(new[] { "owner-a", "owner-b" })),
                null,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new OwnerWorkloadScoreSnapshot("owner-a", 10, 4, 3, 2, 5, 27),
                new OwnerWorkloadScoreSnapshot("owner-b", 2, 0, 0, 0, 0, 5),
            ]);

        var slaConfigurationService = new Mock<ISlaConfigurationService>(MockBehavior.Strict);
        slaConfigurationService
            .Setup(service => service.GetPriorityMapAsync())
            .ReturnsAsync(new Dictionary<string, SlaConfiguration>(StringComparer.OrdinalIgnoreCase)
            {
                ["Critical"] = new() { Priority = "Critical", TargetHours = 8, WarningHours = 2 },
            });

        var service = new OperationalRiskService(
            ownerScoringService.Object,
            slaConfigurationService.Object);

        var ticket = CreateTicket(
            id: "T-100",
            priority: "Critical",
            status: "New",
            createdDateUtc: DateTime.UtcNow.AddHours(-7),
            synitiOwner: "owner-a",
            businessOwner: "owner-b");

        var assessment = await service.EvaluateAsync(ticket);

        Assert.Equal(7, assessment.OperationalRiskScore);
        Assert.Equal("high", assessment.RiskLevel);
        Assert.Contains("SLA is at risk.", assessment.Reasons);
        Assert.Contains("Priority is critical.", assessment.Reasons);
        Assert.Contains("Assigned owner workload pressure is high.", assessment.Reasons);
        Assert.Equal("Review assignment or escalate within 1 hour.", assessment.RecommendedAction);
        Assert.Equal(32, assessment.OwnerPressure.WorkloadScore);
        Assert.Equal("critical", assessment.OwnerPressure.PressureLevel);
        Assert.True(assessment.IsOwnerOverloaded);
        Assert.True(assessment.IsOwnershipComplete);
        Assert.False(assessment.IsAssignmentSafe);
    }

    [Fact]
    public async Task EvaluateAsync_MissingBusinessOwner_AddsOwnershipRiskAndRecommendation()
    {
        var ownerScoringService = new Mock<IOwnerWorkloadScoringService>(MockBehavior.Strict);
        ownerScoringService
            .Setup(service => service.GetScoresAsync(
                It.Is<IEnumerable<string>>(keys => keys.SequenceEqual(new[] { "owner-a" })),
                null,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new OwnerWorkloadScoreSnapshot("owner-a", 2, 0, 0, 0, 0, 2),
            ]);

        var slaConfigurationService = new Mock<ISlaConfigurationService>(MockBehavior.Strict);
        slaConfigurationService
            .Setup(service => service.GetPriorityMapAsync())
            .ReturnsAsync(new Dictionary<string, SlaConfiguration>(StringComparer.OrdinalIgnoreCase)
            {
                ["Low"] = new() { Priority = "Low", TargetHours = 24, WarningHours = 8 },
            });

        var service = new OperationalRiskService(
            ownerScoringService.Object,
            slaConfigurationService.Object);

        var ticket = CreateTicket(
            id: "T-200",
            priority: "Low",
            status: "New",
            createdDateUtc: DateTime.UtcNow,
            synitiOwner: "owner-a",
            businessOwner: null);

        var assessment = await service.EvaluateAsync(ticket);

        Assert.Equal(4, assessment.OperationalRiskScore);
        Assert.Equal("moderate", assessment.RiskLevel);
        Assert.Contains("Business owner is missing.", assessment.Reasons);
        Assert.Equal("Add missing Business Owner.", assessment.RecommendedAction);
        Assert.False(assessment.IsOwnershipComplete);
        Assert.False(assessment.IsAssignmentSafe);
    }

    private static Ticket CreateTicket(
        string id,
        string priority,
        string status,
        DateTime createdDateUtc,
        string? synitiOwner,
        string? businessOwner)
    {
        return new Ticket
        {
            Id = id,
            Title = $"Ticket {id}",
            Description = "Operational risk test ticket",
            Status = status,
            ApprovalStatus = ApprovalStatus.Approved,
            Priority = priority,
            BoardId = 1,
            SynitiOwner = synitiOwner,
            BusinessOwner = businessOwner,
            CreatedBy = 1,
            CreatedDate = createdDateUtc,
            LastModifiedBy = 1,
            LastModifiedDate = createdDateUtc,
        };
    }
}
