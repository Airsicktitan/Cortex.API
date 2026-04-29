using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Moq;

namespace Cortex.API.Tests;

public class CortexSlaRiskServiceTests
{
    [Fact]
    public async Task EvaluateRiskAsync_AddsMemorySignalWithoutEscalatingRisk()
    {
        var service = CreateService();
        var ticket = CreateLowRiskTicket();
        var insight = new CortexInsightDto
        {
            TicketId = ticket.Id,
            ConfidenceScore = 64,
            Matches =
            [
                new CortexInsightSimilarTicketDto
                {
                    Id = "T-200",
                    SourceTicketId = "T-200",
                    Title = "Prior authorization issue",
                    Status = "Resolved",
                    ConfidenceScore = 64,
                },
            ],
            LearningSignals =
            [
                new CortexLearningSignalDto
                {
                    SignalType = "Semantic",
                    Title = "Similar tickets often needed follow-up",
                    Description = "Past tickets similar to this one had higher-than-average comment activity before resolution.",
                    Confidence = "Medium",
                    SupportingFacts =
                    [
                        "5 similar tickets analyzed",
                        "Average comments: 4.2",
                        "2 required reassignment or clarification",
                    ],
                },
            ],
        };

        var assessment = await service.EvaluateRiskAsync(ticket, cachedInsight: insight);

        Assert.Equal(CortexRiskLevel.Low, assessment.RiskLevel);
        Assert.Equal(0, assessment.Score);
        Assert.Contains("Recent similar issues required follow-up", assessment.RiskReasons);
    }

    [Fact]
    public async Task EvaluateRiskAsync_IgnoresLowConfidenceMemoryPatterns()
    {
        var service = CreateService();
        var ticket = CreateLowRiskTicket();
        var insight = new CortexInsightDto
        {
            TicketId = ticket.Id,
            ConfidenceScore = 40,
            Matches =
            [
                new CortexInsightSimilarTicketDto
                {
                    Id = "T-201",
                    SourceTicketId = "T-201",
                    Title = "Loose keyword match",
                    Status = "Resolved",
                    ConfidenceScore = 40,
                },
            ],
            LearningSignals =
            [
                new CortexLearningSignalDto
                {
                    SignalType = "Semantic",
                    Title = "Similar tickets often needed follow-up",
                    Description = "Past tickets similar to this one had higher-than-average comment activity before resolution.",
                    Confidence = "Medium",
                },
            ],
        };

        var assessment = await service.EvaluateRiskAsync(ticket, cachedInsight: insight);

        Assert.DoesNotContain("Recent similar issues required follow-up", assessment.RiskReasons);
        Assert.Contains("No elevated SLA, intake, or workload signals on this ticket.", assessment.RiskReasons);
    }

    [Fact]
    public async Task EvaluateRiskAsync_IgnoresPositiveHistoricalSignals()
    {
        var service = CreateService();
        var ticket = CreateLowRiskTicket();
        var insight = new CortexInsightDto
        {
            TicketId = ticket.Id,
            ConfidenceScore = 82,
            Matches =
            [
                new CortexInsightSimilarTicketDto
                {
                    Id = "T-202",
                    SourceTicketId = "T-202",
                    Title = "Strong delivery history",
                    Status = "Resolved",
                    ConfidenceScore = 82,
                },
            ],
            LearningSignals =
            [
                new CortexLearningSignalDto
                {
                    SignalType = "Owner",
                    Title = "Assigned owner has strong delivery history",
                    Description = "This owner has a consistent record of resolving similar tickets within SLA.",
                    Confidence = "Medium",
                    SupportingFacts = ["88% resolved within SLA"],
                },
            ],
        };

        var assessment = await service.EvaluateRiskAsync(ticket, cachedInsight: insight);

        Assert.DoesNotContain("Recent similar issues required follow-up", assessment.RiskReasons);
        Assert.Contains("No elevated SLA, intake, or workload signals on this ticket.", assessment.RiskReasons);
    }

    private static CortexSlaRiskService CreateService()
    {
        var slaConfigurationService = new Mock<ISlaConfigurationService>(MockBehavior.Strict);
        slaConfigurationService
            .Setup(service => service.GetPriorityMapAsync())
            .ReturnsAsync(new Dictionary<string, SlaConfiguration>(StringComparer.OrdinalIgnoreCase)
            {
                ["Low"] = new() { Priority = "Low", TargetHours = 72, WarningHours = 12 },
            });

        var workloadSnapshotService = new Mock<IWorkloadSnapshotService>(MockBehavior.Strict);

        return new CortexSlaRiskService(
            slaConfigurationService.Object,
            workloadSnapshotService.Object);
    }

    private static Ticket CreateLowRiskTicket() => new()
    {
        Id = "T-100",
        Title = "Authorization setup request",
        Description = "User needs access to complete an approved workflow.",
        Priority = "Low",
        Status = "New",
        ApprovalStatus = ApprovalStatus.Approved,
        CreatedDate = DateTime.UtcNow,
    };
}
