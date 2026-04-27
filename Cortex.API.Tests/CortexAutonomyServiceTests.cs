using System.Text.Json;
using Cortex.API.Configuration;
using Cortex.API.Data;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cortex.API.Tests;

public class CortexAutonomyServiceTests
{
    [Fact]
    public async Task Evaluate_LowConfidence_BlocksWithReason()
    {
        var harness = new Harness();
        harness.DecisionResult.ConfidenceScore = 0.60m;

        var result = await harness.Evaluate();

        Assert.False(result.IsEligible);
        Assert.False(result.WasAutoApplied);
        Assert.Equal("Disabled", result.Mode);
        Assert.Contains(result.BlockedReasons, reason => reason.Contains("Confidence below auto-apply threshold"));
    }

    [Fact]
    public async Task Evaluate_NoRecommendedOwner_Blocks()
    {
        var harness = new Harness();
        harness.DecisionResult.RecommendedOwnerUserId = null;
        harness.DecisionResult.RecommendedOwnerDisplayName = null;

        var result = await harness.Evaluate();

        Assert.False(result.IsEligible);
        Assert.Contains("No recommended owner identified.", result.BlockedReasons);
    }

    [Fact]
    public async Task Evaluate_SameOwner_Blocks()
    {
        var harness = new Harness(currentSynitiOwner: "owner-a");
        harness.DecisionResult.RecommendedOwnerUserId = "owner-a";
        harness.DecisionResult.RecommendedOwnerDisplayName = "Owner Alpha";

        var result = await harness.Evaluate();

        Assert.False(result.IsEligible);
        Assert.Contains("Recommended owner matches the current owner.", result.BlockedReasons);
    }

    [Fact]
    public async Task Evaluate_RecentOverrideWithinWindow_Blocks()
    {
        var harness = new Harness();
        harness.LatestOverride = new TicketRoutingOverride
        {
            Id = 1,
            TicketId = harness.Ticket.Id,
            CreatedDateUtc = DateTime.UtcNow.AddHours(-3),
        };

        var result = await harness.Evaluate();

        Assert.False(result.IsEligible);
        Assert.Contains(result.BlockedReasons, reason => reason.Contains("Recent human override"));
    }

    [Fact]
    public async Task Evaluate_HighOperationalRisk_Blocks()
    {
        var harness = new Harness();
        harness.RiskResponse.RiskLevel = "high";
        harness.RiskResponse.Reasons.Add("SLA is at risk.");

        var result = await harness.Evaluate();

        Assert.False(result.IsEligible);
        Assert.Contains(result.BlockedReasons, reason => reason.Contains("Operational risk is high"));
    }

    [Fact]
    public async Task Evaluate_TerminalStatus_Blocks()
    {
        var harness = new Harness();
        harness.Ticket.Status = "Resolved";

        var result = await harness.Evaluate();

        Assert.False(result.IsEligible);
        Assert.Contains(result.BlockedReasons, reason => reason.Contains("terminal"));
    }

    [Fact]
    public async Task Evaluate_UnclearWinner_Blocks()
    {
        var harness = new Harness();
        harness.DecisionResult.Candidates = new List<CortexDecisionCandidate>
        {
            new() { UserId = "owner-b", DisplayName = "Owner Bravo", Eligible = true, TotalScore = 100 },
            new() { UserId = "owner-c", DisplayName = "Owner Charlie", Eligible = true, TotalScore = 99 },
        };

        var result = await harness.Evaluate();

        Assert.False(result.IsEligible);
        Assert.Contains(result.BlockedReasons, reason => reason.Contains("not clearly ahead of alternatives"));
    }

    [Fact]
    public async Task Evaluate_HappyPath_DefaultConfig_IsEligibleButDisabled_AndDoesNotMutate()
    {
        // Default options have Enabled=false, so the mode is "Disabled" — eligible but never executed.
        var harness = new Harness();

        var beforeOwner = harness.Ticket.SynitiOwner;
        var result = await harness.Evaluate();

        Assert.True(result.IsEligible);
        Assert.False(result.WasAutoApplied);
        Assert.Equal("Disabled", result.Mode);
        Assert.Equal(beforeOwner, harness.Ticket.SynitiOwner);
        harness.TicketRepository.Verify(
            r => r.UpdateTicketAsync(It.IsAny<Ticket>()),
            Times.Never);

        await using var verifyCtx = harness.CreateContext();
        var stored = await verifyCtx.CortexAutonomyDecisions.SingleAsync();
        Assert.True(stored.IsEligible);
        Assert.False(stored.WasAutoApplied);
        Assert.Equal("Disabled", stored.Mode);
        var passed = JsonSerializer.Deserialize<List<string>>(stored.PassedChecksJson);
        Assert.NotNull(passed);
        Assert.NotEmpty(passed);
    }

    [Fact]
    public async Task Evaluate_ShadowMode_IsEligibleAndDoesNotMutate()
    {
        var harness = new Harness();
        harness.Options.Enabled = true;
        harness.Options.ShadowMode = true;

        var beforeOwner = harness.Ticket.SynitiOwner;
        var result = await harness.Evaluate();

        Assert.True(result.IsEligible);
        Assert.False(result.WasAutoApplied);
        Assert.Equal("Shadow", result.Mode);
        Assert.Equal(beforeOwner, harness.Ticket.SynitiOwner);
        harness.TicketRepository.Verify(
            r => r.UpdateTicketAsync(It.IsAny<Ticket>()),
            Times.Never);
    }

    [Fact]
    public async Task Evaluate_DefaultConfig_NeverMutates_EvenWhenEligible()
    {
        // Reaffirms the rule: with Enabled=false (default), no auto-apply ever happens.
        var harness = new Harness();
        harness.Options.Enabled = false;
        harness.Options.ShadowMode = false; // Even if shadow is off, Enabled gates execution.

        var result = await harness.Evaluate();

        Assert.True(result.IsEligible);
        Assert.False(result.WasAutoApplied);
        Assert.Equal("Disabled", result.Mode);
        harness.TicketRepository.Verify(
            r => r.UpdateTicketAsync(It.IsAny<Ticket>()),
            Times.Never);
    }

    [Fact]
    public async Task Evaluate_ExecutionEnabled_AutoApplies_AndRecordsAuditRow()
    {
        var harness = new Harness(currentSynitiOwner: "owner-current");
        harness.Options.Enabled = true;
        harness.Options.ShadowMode = false;

        var result = await harness.Evaluate();

        Assert.True(result.IsEligible);
        Assert.True(result.WasAutoApplied);
        Assert.Equal("AutoApplied", result.Mode);
        Assert.Equal("owner-bravo", harness.Ticket.SynitiOwner);
        harness.TicketRepository.Verify(
            r => r.UpdateTicketAsync(It.Is<Ticket>(t => t.SynitiOwner == "owner-bravo")),
            Times.Once);
        harness.TicketRepository.Verify(r => r.SaveChangesAsync(), Times.Once);

        await using var verifyCtx = harness.CreateContext();
        var stored = await verifyCtx.CortexAutonomyDecisions.SingleAsync();
        Assert.True(stored.WasAutoApplied);
        Assert.NotNull(stored.AppliedDateUtc);
        Assert.Equal("owner-current", stored.PreviousOwnerId);
        Assert.Equal("owner-bravo", stored.RecommendedOwnerId);
    }

    [Fact]
    public async Task GetLatestAsync_ReturnsMostRecentRow()
    {
        var harness = new Harness();
        await using (var seedCtx = harness.CreateContext())
        {
            seedCtx.CortexAutonomyDecisions.Add(new CortexAutonomyDecision
            {
                TicketId = harness.Ticket.Id,
                CreatedDateUtc = DateTime.UtcNow.AddHours(-2),
                Mode = "Shadow",
                Summary = "older",
                Confidence = 0.5m,
                PassedChecksJson = "[]",
                BlockedReasonsJson = "[]",
                DecisionVersion = "autonomy-v1",
            });
            seedCtx.CortexAutonomyDecisions.Add(new CortexAutonomyDecision
            {
                TicketId = harness.Ticket.Id,
                CreatedDateUtc = DateTime.UtcNow,
                Mode = "Shadow",
                Summary = "newest",
                Confidence = 0.9m,
                PassedChecksJson = "[]",
                BlockedReasonsJson = "[]",
                DecisionVersion = "autonomy-v1",
            });
            await seedCtx.SaveChangesAsync();
        }

        var service = harness.BuildService();
        var latest = await service.GetLatestAsync(harness.Ticket.Id);

        Assert.NotNull(latest);
        Assert.Equal("newest", latest!.Summary);
    }

    private sealed class Harness
    {
        public Harness(string? currentSynitiOwner = null)
        {
            DatabaseName = $"autonomy-{Guid.NewGuid():N}";
            Ticket = new Ticket
            {
                Id = "T-1001",
                Title = "Sample",
                Description = "sample",
                Status = "InProgress",
                ApprovalStatus = ApprovalStatus.Approved,
                Priority = "Medium",
                BoardId = 1,
                SynitiOwner = currentSynitiOwner,
                CreatedBy = 1,
                CreatedDate = DateTime.UtcNow,
            };

            Options = new CortexAutonomyOptions();

            DecisionResult = new CortexDecisionResult
            {
                DecisionType = "RecommendAssignment",
                RecommendedOwnerUserId = "owner-bravo",
                RecommendedOwnerDisplayName = "Owner Bravo",
                CurrentOwnerUserId = currentSynitiOwner,
                Summary = "Cortex recommends owner-bravo.",
                ConfidenceScore = 0.92m,
                Candidates =
                {
                    new CortexDecisionCandidate
                    {
                        UserId = "owner-bravo",
                        DisplayName = "Owner Bravo",
                        Eligible = true,
                        TotalScore = 100m,
                    },
                    new CortexDecisionCandidate
                    {
                        UserId = "owner-charlie",
                        DisplayName = "Owner Charlie",
                        Eligible = true,
                        TotalScore = 60m,
                    },
                },
            };

            SettingsService = new Mock<ICortexAutonomySettingsService>(MockBehavior.Loose);
            SettingsService
                .Setup(s => s.GetEffectiveAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => new CortexAutonomyOptions
                {
                    Enabled = Options.Enabled,
                    ShadowMode = Options.ShadowMode,
                    MinConfidence = Options.MinConfidence,
                    RecentOverrideWindowHours = Options.RecentOverrideWindowHours,
                    RequireClearWinner = Options.RequireClearWinner,
                    MinAlternativeGap = Options.MinAlternativeGap,
                });

            DecisionService = new Mock<ICortexDecisionService>(MockBehavior.Strict);
            DecisionService
                .Setup(s => s.EvaluateAssignmentAsync(
                    It.IsAny<Ticket>(),
                    It.IsAny<CortexAiAssessment?>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => DecisionResult);

            RoutingService = new Mock<ITicketRoutingRuleService>(MockBehavior.Loose);
            RoutingService
                .Setup(s => s.GetLatestOverrideAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => LatestOverride);

            RiskResponse = new OperationalRiskResponse
            {
                RiskLevel = "low",
                Reasons = [],
                IsAssignmentSafe = true,
            };
            RiskService = new Mock<IOperationalRiskService>(MockBehavior.Loose);
            RiskService
                .Setup(s => s.EvaluateAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => RiskResponse);

            TicketRepository = new Mock<ITicketRepository>(MockBehavior.Loose);
            TicketRepository
                .Setup(r => r.UpdateTicketAsync(It.IsAny<Ticket>()))
                .ReturnsAsync((Ticket t) => t);
            TicketRepository
                .Setup(r => r.SaveChangesAsync())
                .Returns(Task.CompletedTask);
        }

        public string DatabaseName { get; }
        public Ticket Ticket { get; }
        public CortexAutonomyOptions Options { get; }
        public CortexDecisionResult DecisionResult { get; }
        public TicketRoutingOverride? LatestOverride { get; set; }
        public OperationalRiskResponse RiskResponse { get; }
        public Mock<ICortexAutonomySettingsService> SettingsService { get; }
        public Mock<ICortexDecisionService> DecisionService { get; }
        public Mock<ITicketRoutingRuleService> RoutingService { get; }
        public Mock<IOperationalRiskService> RiskService { get; }
        public Mock<ITicketRepository> TicketRepository { get; }

        public CortexDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<CortexDbContext>()
                .UseInMemoryDatabase(DatabaseName)
                .Options;
            return new CortexDbContext(options);
        }

        public CortexAutonomyService BuildService(CortexDbContext? context = null)
        {
            return new CortexAutonomyService(
                SettingsService.Object,
                DecisionService.Object,
                RoutingService.Object,
                RiskService.Object,
                TicketRepository.Object,
                context ?? CreateContext(),
                NullLogger<CortexAutonomyService>.Instance);
        }

        public async Task<CortexAutonomyResultDto> Evaluate()
        {
            await using var context = CreateContext();
            var service = BuildService(context);
            return await service.EvaluateAndMaybeApplyDecisionAsync(Ticket);
        }
    }
}
