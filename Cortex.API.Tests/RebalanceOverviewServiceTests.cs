using Cortex.API.Data;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Cortex.API.Tests;

/// <summary>
/// Unit tests for the Operational Rebalance overview service. The service
/// composes other scoring/risk/recommendation services — these tests stub
/// those collaborators and focus on what this service owns: overloaded-owner
/// detection, candidate filtering, deterministic ranking, top-N cap, and
/// empty state.
///
/// SLA math is real (TicketSlaCalculator is static). To keep results
/// deterministic, <see cref="SeedTicket"/> backdates CreatedDate by 1 hour
/// and the test priority map gives Low/Medium/High a TargetHours of 240
/// (so they stay "On Track") and Critical a TargetHours of 0 (so they land
/// in "Breached"). Tests that want a breached candidate therefore seed it
/// with Priority="Critical"; tests that want "On Track" use any other
/// priority.
/// </summary>
public class RebalanceOverviewServiceTests
{
    [Fact]
    public async Task GetOverviewAsync_NoTickets_ReturnsEmptyResponse()
    {
        await using var context = CreateContext();

        var service = BuildService(
            context,
            ownerScores: [],
            riskByTicket: new Dictionary<string, OperationalRiskResponse>(StringComparer.Ordinal),
            recommendationsByTicket: new Dictionary<string, ReassignmentRecommendationResponse>(StringComparer.Ordinal));

        var overview = await service.GetOverviewAsync();

        Assert.Empty(overview.OverloadedOwners);
        Assert.Empty(overview.RebalanceCandidates);
    }

    [Fact]
    public async Task GetOverviewAsync_NoOverloadedOwners_ReturnsEmptyResponse()
    {
        await using var context = CreateContext();
        var now = DateTime.UtcNow;

        context.Tickets.Add(SeedTicket("T-1", "owner-a", "Medium", now));
        await context.SaveChangesAsync();

        var service = BuildService(
            context,
            ownerScores:
            [
                new OwnerWorkloadScoreSnapshot("owner-a", 1, 0, 0, 0, 0, 3),
            ],
            riskByTicket: new Dictionary<string, OperationalRiskResponse>(StringComparer.Ordinal));

        var overview = await service.GetOverviewAsync();

        Assert.Empty(overview.OverloadedOwners);
        Assert.Empty(overview.RebalanceCandidates);
    }

    [Fact]
    public async Task GetOverviewAsync_ReturnsOverloadedOwnersSortedByWorkloadDescending()
    {
        await using var context = CreateContext();
        var now = DateTime.UtcNow;

        context.Tickets.Add(SeedTicket("T-A1", "owner-a", "High", now));
        context.Tickets.Add(SeedTicket("T-B1", "owner-b", "High", now));
        context.Tickets.Add(SeedTicket("T-C1", "owner-c", "Low", now));
        await context.SaveChangesAsync();

        // owner-a: critical (35). owner-b: high (25). owner-c: moderate (15) — excluded.
        var service = BuildService(
            context,
            ownerScores:
            [
                new OwnerWorkloadScoreSnapshot("owner-a", 10, 3, 2, 1, 3, 35),
                new OwnerWorkloadScoreSnapshot("owner-b", 7, 2, 1, 0, 1, 25),
                new OwnerWorkloadScoreSnapshot("owner-c", 4, 1, 0, 0, 0, 15),
            ],
            riskByTicket: new Dictionary<string, OperationalRiskResponse>(StringComparer.Ordinal)
            {
                ["T-A1"] = RiskLevel("high"),
                ["T-B1"] = RiskLevel("high"),
                // owner-c's ticket is not evaluated — its owner is not overloaded.
            },
            recommendationsByTicket: new Dictionary<string, ReassignmentRecommendationResponse>(StringComparer.Ordinal)
            {
                ["T-A1"] = SimpleRecommendation("ada", 8),
                ["T-B1"] = SimpleRecommendation("ben", 10),
            },
            users:
            [
                User("owner-a", "Owner A"),
                User("owner-b", "Owner B"),
                User("owner-c", "Owner C"),
            ]);

        var overview = await service.GetOverviewAsync();

        Assert.Equal(2, overview.OverloadedOwners.Count);
        Assert.Equal("owner-a", overview.OverloadedOwners[0].OwnerId);
        Assert.Equal("critical", overview.OverloadedOwners[0].PressureLevel);
        Assert.Equal(35, overview.OverloadedOwners[0].WorkloadScore);
        Assert.Equal("owner-b", overview.OverloadedOwners[1].OwnerId);
        Assert.Equal("high", overview.OverloadedOwners[1].PressureLevel);
        Assert.DoesNotContain(
            overview.OverloadedOwners,
            summary => summary.OwnerId == "owner-c");
        // Owner key "owner-a" does not match DisplayName/email normalization in BuildUserLookup; name falls back to the key.
        Assert.Equal("owner-a", overview.OverloadedOwners[0].OwnerName);
    }

    [Fact]
    public async Task GetOverviewAsync_FiltersCandidatesToHighRiskOrSlaRisk()
    {
        await using var context = CreateContext();
        var now = DateTime.UtcNow;

        // All four tickets belong to an overloaded owner. Only two should
        // survive the "actionable" filter.
        //
        //   T-LOW       → low op risk,   safe SLA      → excluded
        //   T-MOD-SAFE  → moderate risk, safe SLA      → excluded
        //   T-HIGH      → high op risk,  safe SLA      → included (op)
        //   T-MOD-SLA   → moderate risk, breached SLA  → included (SLA)
        context.Tickets.Add(SeedTicket("T-LOW", "owner-a", "Low", now));
        context.Tickets.Add(SeedTicket("T-MOD-SAFE", "owner-a", "Medium", now));
        context.Tickets.Add(SeedTicket("T-HIGH", "owner-a", "High", now));
        context.Tickets.Add(SeedTicket("T-MOD-SLA", "owner-a", "Critical", now));
        await context.SaveChangesAsync();

        var service = BuildService(
            context,
            ownerScores:
            [
                new OwnerWorkloadScoreSnapshot("owner-a", 12, 3, 2, 1, 3, 32),
            ],
            riskByTicket: new Dictionary<string, OperationalRiskResponse>(StringComparer.Ordinal)
            {
                ["T-LOW"] = RiskLevel("low"),
                ["T-MOD-SAFE"] = RiskLevel("moderate"),
                ["T-HIGH"] = RiskLevel("high"),
                ["T-MOD-SLA"] = RiskLevel("moderate"),
            },
            recommendationsByTicket: new Dictionary<string, ReassignmentRecommendationResponse>(StringComparer.Ordinal)
            {
                ["T-HIGH"] = SimpleRecommendation("ada", 5),
                ["T-MOD-SLA"] = SimpleRecommendation("ada", 5),
            });

        var overview = await service.GetOverviewAsync();

        var ticketIds = overview.RebalanceCandidates.Select(c => c.TicketId).ToHashSet();
        Assert.Equal(2, ticketIds.Count);
        Assert.Contains("T-HIGH", ticketIds);
        Assert.Contains("T-MOD-SLA", ticketIds);
        Assert.DoesNotContain("T-LOW", ticketIds);
        Assert.DoesNotContain("T-MOD-SAFE", ticketIds);

        var slaCandidate = overview.RebalanceCandidates.Single(c => c.TicketId == "T-MOD-SLA");
        Assert.Equal("breached", slaCandidate.SlaRiskLevel);
        Assert.Equal("moderate", slaCandidate.OperationalRiskLevel);
    }

    [Fact]
    public async Task GetOverviewAsync_RanksCandidatesDeterministically()
    {
        await using var context = CreateContext();
        var now = DateTime.UtcNow;

        // Expected ordering:
        //   1. T-CRIT        (critical op, breached SLA by virtue of Priority=Critical)
        //   2. T-HIGH-BREACH (high op, breached SLA — Priority=Critical)
        //   3. T-HIGH-B      (high op, safe SLA, owner workload 32)
        //   4. T-HIGH-A      (high op, safe SLA, owner workload 22)
        context.Tickets.Add(SeedTicket("T-CRIT", "owner-a", "Critical", now));
        context.Tickets.Add(SeedTicket("T-HIGH-BREACH", "owner-a", "Critical", now));
        context.Tickets.Add(SeedTicket("T-HIGH-A", "owner-a", "High", now));
        context.Tickets.Add(SeedTicket("T-HIGH-B", "owner-b", "High", now));
        await context.SaveChangesAsync();

        var service = BuildService(
            context,
            ownerScores:
            [
                new OwnerWorkloadScoreSnapshot("owner-a", 7, 2, 0, 0, 0, 22),
                new OwnerWorkloadScoreSnapshot("owner-b", 10, 3, 1, 0, 1, 32),
            ],
            riskByTicket: new Dictionary<string, OperationalRiskResponse>(StringComparer.Ordinal)
            {
                ["T-CRIT"] = RiskLevel("critical"),
                ["T-HIGH-BREACH"] = RiskLevel("high"),
                ["T-HIGH-A"] = RiskLevel("high"),
                ["T-HIGH-B"] = RiskLevel("high"),
            },
            recommendationsByTicket: new Dictionary<string, ReassignmentRecommendationResponse>(StringComparer.Ordinal)
            {
                ["T-CRIT"] = SimpleRecommendation("ada", 5),
                ["T-HIGH-BREACH"] = SimpleRecommendation("ada", 5),
                ["T-HIGH-A"] = SimpleRecommendation("ada", 5),
                ["T-HIGH-B"] = SimpleRecommendation("ada", 5),
            });

        var overview = await service.GetOverviewAsync();

        Assert.Equal(
            new[] { "T-CRIT", "T-HIGH-BREACH", "T-HIGH-B", "T-HIGH-A" },
            overview.RebalanceCandidates.Select(c => c.TicketId).ToArray());
    }

    [Fact]
    public async Task GetOverviewAsync_StableTieBreak_OrdersByTicketIdAscending()
    {
        await using var context = CreateContext();
        var now = DateTime.UtcNow;

        // Same owner, same priority, same everything — ids must break the tie.
        context.Tickets.Add(SeedTicket("T-003", "owner-a", "High", now));
        context.Tickets.Add(SeedTicket("T-001", "owner-a", "High", now));
        context.Tickets.Add(SeedTicket("T-002", "owner-a", "High", now));
        await context.SaveChangesAsync();

        var service = BuildService(
            context,
            ownerScores:
            [
                new OwnerWorkloadScoreSnapshot("owner-a", 10, 3, 0, 0, 0, 25),
            ],
            riskByTicket: new Dictionary<string, OperationalRiskResponse>(StringComparer.Ordinal)
            {
                ["T-001"] = RiskLevel("high"),
                ["T-002"] = RiskLevel("high"),
                ["T-003"] = RiskLevel("high"),
            },
            recommendationsByTicket: new Dictionary<string, ReassignmentRecommendationResponse>(StringComparer.Ordinal)
            {
                ["T-001"] = SimpleRecommendation("ada", 5),
                ["T-002"] = SimpleRecommendation("ada", 5),
                ["T-003"] = SimpleRecommendation("ada", 5),
            });

        var overview = await service.GetOverviewAsync();

        Assert.Equal(
            new[] { "T-001", "T-002", "T-003" },
            overview.RebalanceCandidates.Select(c => c.TicketId).ToArray());
    }

    [Fact]
    public async Task GetOverviewAsync_CapsCandidatesAtTwenty()
    {
        await using var context = CreateContext();
        var now = DateTime.UtcNow;

        var riskByTicket = new Dictionary<string, OperationalRiskResponse>(StringComparer.Ordinal);
        var recByTicket = new Dictionary<string, ReassignmentRecommendationResponse>(StringComparer.Ordinal);
        for (int i = 1; i <= 30; i++)
        {
            var id = $"T-{i:D3}";
            context.Tickets.Add(SeedTicket(id, "owner-a", "High", now));
            riskByTicket[id] = RiskLevel("high");
            recByTicket[id] = SimpleRecommendation("ada", 5);
        }
        await context.SaveChangesAsync();

        var service = BuildService(
            context,
            ownerScores:
            [
                new OwnerWorkloadScoreSnapshot("owner-a", 30, 10, 0, 0, 0, 50),
            ],
            riskByTicket: riskByTicket,
            recommendationsByTicket: recByTicket);

        var overview = await service.GetOverviewAsync();

        Assert.Equal(20, overview.RebalanceCandidates.Count);
        Assert.Equal("T-001", overview.RebalanceCandidates[0].TicketId);
        Assert.Equal("T-020", overview.RebalanceCandidates[^1].TicketId);
    }

    [Fact]
    public async Task GetOverviewAsync_ExcludesArchivedAndResolvedTickets()
    {
        await using var context = CreateContext();
        var now = DateTime.UtcNow;

        context.Tickets.Add(SeedTicket("T-OPEN", "owner-a", "High", now));
        context.Tickets.Add(SeedTicket("T-ARCHIVED", "owner-a", "High", now));
        context.Tickets.Add(SeedTicket("T-RESOLVED", "owner-a", "High", now, status: "Resolved"));

        context.ArchivedTickets.Add(new ArchivedTicket
        {
            Id = "T-ARCHIVED",
            Title = "Archived",
            Description = "Archived ticket",
            Status = "New",
            Priority = "High",
            BoardId = 1,
            CreatedBy = 1,
            CreatedDate = now,
            LastModifiedBy = 1,
            ArchivedBy = 1,
            ArchivedDate = now,
            CommentCount = 0,
            AttachmentCount = 0,
        });
        await context.SaveChangesAsync();

        var service = BuildService(
            context,
            ownerScores:
            [
                new OwnerWorkloadScoreSnapshot("owner-a", 1, 1, 0, 0, 0, 22),
            ],
            riskByTicket: new Dictionary<string, OperationalRiskResponse>(StringComparer.Ordinal)
            {
                ["T-OPEN"] = RiskLevel("high"),
            },
            recommendationsByTicket: new Dictionary<string, ReassignmentRecommendationResponse>(StringComparer.Ordinal)
            {
                ["T-OPEN"] = SimpleRecommendation("ada", 5),
            });

        var overview = await service.GetOverviewAsync();

        var candidate = Assert.Single(overview.RebalanceCandidates);
        Assert.Equal("T-OPEN", candidate.TicketId);
    }

    [Fact]
    public async Task GetOverviewAsync_CountsHighRiskTicketsPerOwner()
    {
        await using var context = CreateContext();
        var now = DateTime.UtcNow;

        context.Tickets.Add(SeedTicket("T-1", "owner-a", "High", now));
        context.Tickets.Add(SeedTicket("T-2", "owner-a", "High", now));
        context.Tickets.Add(SeedTicket("T-3", "owner-a", "Low", now));
        await context.SaveChangesAsync();

        var service = BuildService(
            context,
            ownerScores:
            [
                new OwnerWorkloadScoreSnapshot("owner-a", 5, 2, 1, 0, 1, 22),
            ],
            riskByTicket: new Dictionary<string, OperationalRiskResponse>(StringComparer.Ordinal)
            {
                ["T-1"] = RiskLevel("critical"),
                ["T-2"] = RiskLevel("high"),
                ["T-3"] = RiskLevel("low"),
            },
            recommendationsByTicket: new Dictionary<string, ReassignmentRecommendationResponse>(StringComparer.Ordinal)
            {
                ["T-1"] = SimpleRecommendation("ada", 5),
                ["T-2"] = SimpleRecommendation("ada", 5),
            });

        var overview = await service.GetOverviewAsync();

        var ownerSummary = Assert.Single(overview.OverloadedOwners);
        Assert.Equal(2, ownerSummary.HighRiskTicketCount);
    }

    // -------- helpers --------

    private static RebalanceOverviewService BuildService(
        CortexDbContext context,
        IReadOnlyList<OwnerWorkloadScoreSnapshot> ownerScores,
        IReadOnlyDictionary<string, OperationalRiskResponse> riskByTicket,
        IReadOnlyDictionary<string, ReassignmentRecommendationResponse>? recommendationsByTicket = null,
        IReadOnlyList<User>? users = null)
    {
        var visibility = new Mock<ITicketVisibilityService>(MockBehavior.Strict);
        visibility
            .Setup(v => v.GetCurrentVisibilityAsync())
            .ReturnsAsync(new TicketVisibilityContext(
                UserId: 0,
                DisplayName: null,
                Email: null,
                Scope: TicketVisibilityScope.All));

        // Priority map: Low/Medium/High stay "On Track" for a ticket created
        // 1h ago (240h target). Critical lands in "Breached" (0h target).
        var priorityMap = new Dictionary<string, SlaConfiguration>(StringComparer.OrdinalIgnoreCase)
        {
            ["Low"] = new() { Priority = "Low", TargetHours = 240, WarningHours = 48 },
            ["Medium"] = new() { Priority = "Medium", TargetHours = 240, WarningHours = 48 },
            ["High"] = new() { Priority = "High", TargetHours = 240, WarningHours = 48 },
            ["Critical"] = new() { Priority = "Critical", TargetHours = 0, WarningHours = 0 },
        };
        var sla = new Mock<ISlaConfigurationService>(MockBehavior.Strict);
        sla
            .Setup(s => s.GetPriorityMapAsync())
            .ReturnsAsync(priorityMap);

        var ownerScoring = new Mock<IOwnerWorkloadScoringService>(MockBehavior.Strict);
        ownerScoring
            .Setup(s => s.GetScoresAsync(
                It.IsAny<IEnumerable<string>>(),
                It.IsAny<string?>(),
                It.IsAny<bool>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ownerScores);

        var operationalRisk = new Mock<IOperationalRiskService>(MockBehavior.Strict);
        operationalRisk
            .Setup(s => s.EvaluateBatchAsync(
                It.IsAny<IEnumerable<Ticket>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Ticket> tickets, CancellationToken _) =>
            {
                var lookup = new Dictionary<string, OperationalRiskResponse>(StringComparer.Ordinal);
                foreach (var ticket in tickets)
                {
                    lookup[ticket.Id] = riskByTicket.TryGetValue(ticket.Id, out var risk)
                        ? risk
                        : RiskLevel("low");
                }
                return (IReadOnlyDictionary<string, OperationalRiskResponse>)lookup;
            });

        var reassignmentRecommendations =
            recommendationsByTicket ?? new Dictionary<string, ReassignmentRecommendationResponse>(StringComparer.Ordinal);
        var reassignment = new Mock<IReassignmentRecommendationService>(MockBehavior.Strict);
        reassignment
            .Setup(s => s.EvaluateBatchAsync(
                It.IsAny<IEnumerable<Ticket>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((IEnumerable<Ticket> tickets, CancellationToken _) =>
            {
                var lookup = new Dictionary<string, ReassignmentRecommendationResponse>(StringComparer.Ordinal);
                foreach (var ticket in tickets)
                {
                    if (reassignmentRecommendations.TryGetValue(ticket.Id, out var rec))
                    {
                        lookup[ticket.Id] = rec;
                    }
                }
                return (IReadOnlyDictionary<string, ReassignmentRecommendationResponse>)lookup;
            });

        var userRepository = new Mock<IUserRepository>(MockBehavior.Strict);
        userRepository
            .Setup(r => r.GetAllUsersAsync())
            .ReturnsAsync(users ?? CreateUnmatchedEligibleUsers(ownerScores.Count));

        return new RebalanceOverviewService(
            context,
            visibility.Object,
            sla.Object,
            ownerScoring.Object,
            operationalRisk.Object,
            reassignment.Object,
            userRepository.Object);
    }

    private static OperationalRiskResponse RiskLevel(string level) => new()
    {
        OperationalRiskScore = level switch
        {
            "critical" => 10,
            "high" => 7,
            "moderate" => 4,
            _ => 0,
        },
        RiskLevel = level,
        Reasons = [],
        RecommendedAction = "Test",
        OwnerPressure = new OwnerPressureResponse
        {
            WorkloadScore = 0,
            PressureLevel = "high",
        },
        IsAssignmentSafe = false,
        IsOwnerOverloaded = true,
        IsOwnershipComplete = true,
    };

    private static ReassignmentRecommendationResponse SimpleRecommendation(
        string targetOwnerKey,
        int targetWorkloadScore)
    {
        return new ReassignmentRecommendationResponse
        {
            ShouldSuggestReassignment = true,
            Reason = "test",
            AssignmentField = "synitiOwner",
            CurrentOwner = new ReassignmentOwnerSnapshotResponse
            {
                OwnerKey = "owner-a",
                DisplayName = "owner-a",
                WorkloadScore = 30,
                PressureLevel = "high",
            },
            SuggestedTargets =
            [
                new ReassignmentTargetResponse
                {
                    OwnerKey = targetOwnerKey,
                    DisplayName = targetOwnerKey,
                    WorkloadScore = targetWorkloadScore,
                    PressureLevel = "low",
                    IsBetterThanCurrent = true,
                    ImprovementReason = "Lower workload among eligible assignees.",
                },
            ],
        };
    }

    private static User User(string key, string displayName) => new()
    {
        Id = Math.Abs(key.GetHashCode()) % 100000,
        DisplayName = displayName,
        Email = $"{key}@example.com",
        Role = Auth0Roles.Developer,
        IsActive = true,
        IsSynitiOwnerEligible = true,
        IsBusinessOwnerEligible = true,
    };

    private static IReadOnlyList<User> CreateUnmatchedEligibleUsers(int count)
    {
        var users = new List<User>(count);
        for (var i = 1; i <= count; i++)
        {
            users.Add(new User
            {
                Id = i,
                DisplayName = $"Unmatched eligible owner {i}",
                Email = $"unmatched-{i}@example.com",
                Role = Auth0Roles.Developer,
                IsActive = true,
                IsSynitiOwnerEligible = true,
                IsBusinessOwnerEligible = true,
            });
        }

        return users;
    }

    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"rebalance-overview-{Guid.NewGuid():N}")
            .Options;

        return new CortexDbContext(options);
    }

    /// <summary>
    /// Tickets are backdated 1 hour to make SLA math deterministic. The test
    /// priority map gives Low/Medium/High a 240h target (so a ticket created
    /// 1 hour ago sits comfortably in "On Track") and Critical a 0h target
    /// (so the same ticket lands squarely in "Breached").
    /// </summary>
    private static Ticket SeedTicket(
        string id,
        string synitiOwner,
        string priority,
        DateTime nowUtc,
        string status = "New")
    {
        var createdDateUtc = nowUtc.AddHours(-1);
        return new Ticket
        {
            Id = id,
            Title = $"Ticket {id}",
            Description = "Rebalance test ticket",
            Status = status,
            ApprovalStatus = ApprovalStatus.Approved,
            Priority = priority,
            BoardId = 1,
            SynitiOwner = synitiOwner,
            BusinessOwner = null,
            CreatedBy = 1,
            CreatedDate = createdDateUtc,
            LastModifiedBy = 1,
            LastModifiedDate = createdDateUtc,
        };
    }
}
