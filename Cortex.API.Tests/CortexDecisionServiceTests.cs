using Cortex.API.Database;
using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Cortex.API.Tests;

public class CortexDecisionServiceTests
{
    [Fact]
    public async Task EvaluateAssignmentAsync_NoCandidates_ReturnsNoEligibleOwner()
    {
        await using var context = CreateContext();
        var candidateService = new Mock<ICortexCandidateResolutionService>(MockBehavior.Strict);
        candidateService
            .Setup(service => service.GetEligibleCandidatesAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        var workloadService = new Mock<IWorkloadSnapshotService>(MockBehavior.Strict);

        var service = CreateService(context, candidateService.Object, workloadService.Object);
        var result = await service.EvaluateAssignmentAsync(CreateTicket("T-1", null));

        Assert.Equal("NoEligibleOwner", result.DecisionType);
        Assert.Equal(0m, result.ConfidenceScore);
    }

    [Fact]
    public async Task EvaluateAssignmentAsync_CurrentOwnerBest_KeepsOwner()
    {
        await using var context = CreateContext();
        var current = Candidate("owner-a", score: 2, high: 0, sla: 0, rule: true, overload: false);
        var challenger = Candidate("owner-b", score: 6, high: 2, sla: 1, rule: true, overload: false);
        var candidateService = new Mock<ICortexCandidateResolutionService>(MockBehavior.Strict);
        candidateService
            .Setup(service => service.GetEligibleCandidatesAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([current, challenger]);
        var workloadService = new Mock<IWorkloadSnapshotService>(MockBehavior.Strict);

        var service = CreateService(context, candidateService.Object, workloadService.Object);
        var result = await service.EvaluateAssignmentAsync(CreateTicket("T-2", "owner-a"));

        Assert.Equal("KeepCurrentOwner", result.DecisionType);
        Assert.Equal("owner-a", result.RecommendedOwnerUserId);
    }

    [Fact]
    public async Task EvaluateRebalanceAsync_BetterCandidate_RecommendsRebalance()
    {
        await using var context = CreateContext();
        var current = Candidate("owner-a", score: 12, high: 3, sla: 2, rule: false, overload: true);
        var better = Candidate("owner-b", score: 1, high: 0, sla: 0, rule: true, overload: false);
        var candidateService = new Mock<ICortexCandidateResolutionService>(MockBehavior.Strict);
        candidateService
            .Setup(service => service.GetEligibleCandidatesAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([current, better]);
        var workloadService = new Mock<IWorkloadSnapshotService>(MockBehavior.Strict);

        var service = CreateService(context, candidateService.Object, workloadService.Object);
        var result = await service.EvaluateRebalanceAsync(CreateTicket("T-3", "owner-a"));

        Assert.Equal("RecommendRebalance", result.DecisionType);
        Assert.Equal("owner-b", result.RecommendedOwnerUserId);
    }

    [Fact]
    public async Task GetRebalanceSuggestionsAsync_FiltersToRecommendRebalance_AndLimitsTopFive()
    {
        await using var context = CreateContext();
        for (var i = 1; i <= 7; i++)
        {
            context.Tickets.Add(CreateTicket($"T-{i}", "owner-a"));
        }
        await context.SaveChangesAsync();

        var candidateService = new Mock<ICortexCandidateResolutionService>(MockBehavior.Strict);
        candidateService
            .Setup(service => service.GetEligibleCandidatesAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ticket ticket, CancellationToken _) =>
            [
                Candidate("owner-a", score: 12, high: 2, sla: 2, rule: false, overload: true),
                Candidate($"owner-b-{ticket.Id}", score: 1, high: 0, sla: 0, rule: true, overload: false),
            ]);

        var workloadService = new Mock<IWorkloadSnapshotService>(MockBehavior.Strict);
        workloadService
            .Setup(service => service.GetSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new WorkloadSnapshot
            {
                UserId = "owner-a",
                DisplayName = "owner-a",
                Status = "Overloaded"
            }]);

        var service = CreateService(context, candidateService.Object, workloadService.Object);
        var suggestions = await service.GetRebalanceSuggestionsAsync();

        Assert.Equal(5, suggestions.Count);
        Assert.All(suggestions, suggestion => Assert.Equal("owner-a", suggestion.FromUserId));
    }

    [Fact]
    public async Task GetRebalanceSuggestionsAsync_ExcludesResolvedAndClosedTickets()
    {
        await using var context = CreateContext();
        context.Tickets.AddRange(
            CreateTicket("T-1", "owner-a"),
            CreateTicket("T-2", "owner-a", status: "Resolved"),
            CreateTicket("T-3", "owner-a", status: "Closed"));
        await context.SaveChangesAsync();

        var candidateService = new Mock<ICortexCandidateResolutionService>(MockBehavior.Strict);
        candidateService
            .Setup(service => service.GetEligibleCandidatesAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ticket ticket, CancellationToken _) =>
            [
                Candidate("owner-a", score: 12, high: 2, sla: 2, rule: false, overload: true),
                Candidate($"owner-b-{ticket.Id}", score: 1, high: 0, sla: 0, rule: true, overload: false),
            ]);

        var workloadService = new Mock<IWorkloadSnapshotService>(MockBehavior.Strict);
        workloadService
            .Setup(service => service.GetSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new WorkloadSnapshot
            {
                UserId = "owner-a",
                DisplayName = "owner-a",
                Status = "Overloaded"
            }]);

        var service = CreateService(context, candidateService.Object, workloadService.Object);
        var suggestions = await service.GetRebalanceSuggestionsAsync();

        Assert.Single(suggestions);
        Assert.Equal("T-1", suggestions[0].TicketId);
    }

    [Fact]
    public async Task GetRebalanceSuggestionsAsync_WhenAiAssessmentThrows_UsesDeterministicFallbackPerTicket()
    {
        await using var context = CreateContext();
        context.Tickets.AddRange(
            CreateTicket("T-1", "owner-a"),
            CreateTicket("T-2", "owner-a"));
        await context.SaveChangesAsync();

        var candidateService = new Mock<ICortexCandidateResolutionService>(MockBehavior.Strict);
        candidateService
            .Setup(service => service.GetEligibleCandidatesAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Ticket ticket, CancellationToken _) =>
            [
                Candidate("owner-a", score: 14, high: 2, sla: 2, rule: false, overload: true),
                Candidate($"owner-b-{ticket.Id}", score: 1, high: 0, sla: 0, rule: true, overload: false),
            ]);

        var workloadService = new Mock<IWorkloadSnapshotService>(MockBehavior.Strict);
        workloadService
            .Setup(service => service.GetSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new WorkloadSnapshot
            {
                UserId = "owner-a",
                DisplayName = "owner-a",
                Status = "Overloaded"
            }]);

        var aiAssessmentService = new Mock<ICortexAiAssessmentService>(MockBehavior.Strict);
        aiAssessmentService
            .Setup(service => service.AssessTicketAsync(
                It.Is<Ticket>(ticket => ticket.Id == "T-1"),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("AI unavailable"));
        aiAssessmentService
            .Setup(service => service.AssessTicketAsync(
                It.Is<Ticket>(ticket => ticket.Id == "T-2"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CortexAiAssessment
            {
                Summary = "High-risk work",
                RiskLevel = "High",
                RecommendedPriority = "High",
                ConfidenceScore = 0.9m
            });

        var service = CreateService(
            context,
            candidateService.Object,
            workloadService.Object,
            aiAssessmentService: aiAssessmentService.Object);

        var suggestions = await service.GetRebalanceSuggestionsAsync();

        Assert.Equal(2, suggestions.Count);
        Assert.Contains(suggestions, suggestion => suggestion.TicketId == "T-1");
        Assert.Contains(suggestions, suggestion => suggestion.TicketId == "T-2");
        Assert.True(suggestions.Single(suggestion => suggestion.TicketId == "T-2").AiHighRisk);
        Assert.False(suggestions.Single(suggestion => suggestion.TicketId == "T-1").AiHighRisk);
    }

    [Fact]
    public async Task GetRebalanceSuggestionsAsync_WhenPerTicketEvaluationThrows_SkipsOnlyFailedTicket()
    {
        await using var context = CreateContext();
        context.Tickets.AddRange(
            CreateTicket("T-1", "owner-a"),
            CreateTicket("T-2", "owner-a"));
        await context.SaveChangesAsync();

        var candidateService = new Mock<ICortexCandidateResolutionService>(MockBehavior.Strict);
        candidateService
            .Setup(service => service.GetEligibleCandidatesAsync(
                It.Is<Ticket>(ticket => ticket.Id == "T-1"),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Candidate resolution error"));
        candidateService
            .Setup(service => service.GetEligibleCandidatesAsync(
                It.Is<Ticket>(ticket => ticket.Id == "T-2"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Candidate("owner-a", score: 12, high: 2, sla: 2, rule: false, overload: true),
                Candidate("owner-b", score: 1, high: 0, sla: 0, rule: true, overload: false),
            ]);

        var workloadService = new Mock<IWorkloadSnapshotService>(MockBehavior.Strict);
        workloadService
            .Setup(service => service.GetSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new WorkloadSnapshot
            {
                UserId = "owner-a",
                DisplayName = "owner-a",
                Status = "Overloaded"
            }]);

        var service = CreateService(context, candidateService.Object, workloadService.Object);
        var suggestions = await service.GetRebalanceSuggestionsAsync();

        Assert.Single(suggestions);
        Assert.Equal("T-2", suggestions[0].TicketId);
    }

    [Fact]
    public async Task GetRebalanceSuggestionsAsync_DoesNotSuggestWhenCurrentOwnerIsOnlyCandidate()
    {
        await using var context = CreateContext();
        context.Tickets.Add(CreateTicket("T-1", "owner-a"));
        await context.SaveChangesAsync();

        var candidateService = new Mock<ICortexCandidateResolutionService>(MockBehavior.Strict);
        candidateService
            .Setup(service => service.GetEligibleCandidatesAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Candidate("owner-a", score: 1, high: 0, sla: 0, rule: true, overload: false),
            ]);

        var workloadService = new Mock<IWorkloadSnapshotService>(MockBehavior.Strict);
        workloadService
            .Setup(service => service.GetSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new WorkloadSnapshot
            {
                UserId = "owner-a",
                DisplayName = "owner-a",
                Status = "Overloaded"
            }]);

        var service = CreateService(context, candidateService.Object, workloadService.Object);
        var suggestions = await service.GetRebalanceSuggestionsAsync();

        Assert.Empty(suggestions);
    }

    [Fact]
    public async Task GetRebalanceSuggestionsAsync_SkipsEquivalentOwnerByDisplayNameAndUserIdMismatch()
    {
        await using var context = CreateContext();
        context.Tickets.Add(CreateTicket("T-1", "Adam Hooper"));
        await context.SaveChangesAsync();

        var candidateService = new Mock<ICortexCandidateResolutionService>(MockBehavior.Strict);
        candidateService
            .Setup(service => service.GetEligibleCandidatesAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Candidate("auth0|adam", score: 1, high: 0, sla: 0, rule: true, overload: false, displayName: "Adam Hooper"),
                Candidate("owner-b", score: 2, high: 0, sla: 0, rule: true, overload: false, displayName: "Other Owner"),
            ]);

        var workloadService = new Mock<IWorkloadSnapshotService>(MockBehavior.Strict);
        workloadService
            .Setup(service => service.GetSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new WorkloadSnapshot
            {
                UserId = "Adam Hooper",
                DisplayName = "Adam Hooper",
                Status = "Overloaded"
            }]);

        var service = CreateService(context, candidateService.Object, workloadService.Object);
        var suggestions = await service.GetRebalanceSuggestionsAsync();

        Assert.Single(suggestions);
        Assert.Equal("owner-b", suggestions[0].ToUserId);
        Assert.NotEqual("Adam Hooper", suggestions[0].ToDisplayName);
    }

    [Fact]
    public async Task ExecuteRebalanceAsync_AppliesValidSuggestions_AndSkipsStaleOnes()
    {
        await using var context = CreateContext();
        context.Tickets.AddRange(
            CreateTicket("T-1", "owner-a"),
            CreateTicket("T-2", "owner-a"));
        await context.SaveChangesAsync();

        var candidateService = new Mock<ICortexCandidateResolutionService>(MockBehavior.Strict);
        candidateService
            .Setup(service => service.GetEligibleCandidatesAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                Candidate("owner-a", score: 12, high: 2, sla: 2, rule: false, overload: true),
                Candidate("owner-b", score: 1, high: 0, sla: 0, rule: true, overload: false),
            ]);

        var workloadService = new Mock<IWorkloadSnapshotService>(MockBehavior.Strict);
        workloadService
            .Setup(service => service.GetSnapshotsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([new WorkloadSnapshot
            {
                UserId = "owner-a",
                DisplayName = "owner-a",
                Status = "Overloaded"
            }]);

        var realtimeService = new Mock<IRealtimeEventService>(MockBehavior.Strict);
        realtimeService
            .Setup(service => service.PublishAsync(It.IsAny<RealtimeEventMessage>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var audienceResolver = new Mock<IRealtimeAudienceResolver>(MockBehavior.Strict);
        audienceResolver
            .Setup(service => service.GetAudienceUserIdsAsync(It.IsAny<Ticket>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([1]);
        var routingRuleService = new Mock<ITicketRoutingRuleService>(MockBehavior.Strict);
        routingRuleService
            .Setup(service => service.GetLatestOverrideAsync("T-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((TicketRoutingOverride?)null);
        routingRuleService
            .Setup(service => service.GetLatestOverrideAsync("T-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketRoutingOverride
            {
                TicketId = "T-2",
                NewSynitiOwner = "owner-a",
                CreatedDateUtc = DateTime.UtcNow,
            });
        var ticketRepository = new Mock<ITicketRepository>(MockBehavior.Strict);
        ticketRepository
            .Setup(repository => repository.GetTicketByIdAsync(It.IsAny<string>()))
            .ReturnsAsync((string id) =>
                context.Tickets.FirstOrDefault(ticket => ticket.Id == id));
        ticketRepository
            .Setup(repository => repository.UpdateTicketAsync(It.IsAny<Ticket>()))
            .ReturnsAsync((Ticket ticket) => ticket);
        ticketRepository
            .Setup(repository => repository.SaveChangesAsync())
            .Returns(() => context.SaveChangesAsync());

        var service = CreateService(
            context,
            candidateService.Object,
            workloadService.Object,
            ticketRepository: ticketRepository.Object,
            realtimeService: realtimeService.Object,
            audienceResolver: audienceResolver.Object,
            routingRuleService: routingRuleService.Object);

        var result = await service.ExecuteRebalanceAsync();

        Assert.Equal(2, result.TotalEvaluated);
        Assert.Equal(1, result.TotalApplied);
        Assert.Single(result.Applied);
        Assert.Single(result.Skipped);
        Assert.Equal("T-1", result.Applied[0].TicketId);
        Assert.Equal("owner-b", result.Applied[0].ToUserId);
        Assert.Equal("T-2", result.Skipped[0].TicketId);
    }

    private static CortexDecisionService CreateService(
        CortexDbContext context,
        ICortexCandidateResolutionService candidateService,
        IWorkloadSnapshotService workloadService,
        ICortexAiAssessmentService? aiAssessmentService = null,
        ITicketRepository? ticketRepository = null,
        ITicketRoutingRuleService? routingRuleService = null,
        IRealtimeEventService? realtimeService = null,
        IRealtimeAudienceResolver? audienceResolver = null)
    {
        var assessment = aiAssessmentService ?? new Mock<ICortexAiAssessmentService>(MockBehavior.Loose).Object;
        var repository = ticketRepository ?? new Mock<ITicketRepository>(MockBehavior.Loose).Object;
        var routing = routingRuleService ?? new Mock<ITicketRoutingRuleService>(MockBehavior.Loose).Object;
        var realtime = realtimeService ?? new Mock<IRealtimeEventService>(MockBehavior.Loose).Object;
        var audience = audienceResolver ?? new Mock<IRealtimeAudienceResolver>(MockBehavior.Loose).Object;

        return new CortexDecisionService(
            context,
            candidateService,
            workloadService,
            assessment,
            repository,
            routing,
            realtime,
            audience);
    }

    private static Ticket CreateTicket(string id, string? synitiOwner, string status = "New")
    {
        return new Ticket
        {
            Id = id,
            Title = id,
            Description = "test",
            Status = status,
            ApprovalStatus = ApprovalStatus.Approved,
            Priority = "High",
            BoardId = 1,
            SynitiOwner = synitiOwner,
            CreatedBy = 1,
            CreatedDate = DateTime.UtcNow
        };
    }

    private static CortexDecisionCandidate Candidate(
        string owner,
        int score,
        int high,
        int sla,
        bool rule,
        bool overload,
        string? displayName = null)
    {
        return new CortexDecisionCandidate
        {
            UserId = owner,
            DisplayName = displayName ?? owner,
            Eligible = true,
            WorkloadScore = score,
            HighPriorityCount = high,
            SlaRiskCount = sla,
            RuleMatched = rule,
            PreferredByBoard = rule,
            CurrentlyOverloaded = overload
        };
    }

    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"cortex-decision-{Guid.NewGuid():N}")
            .Options;
        return new CortexDbContext(options);
    }
}
