using Cortex.API.Database;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cortex.API.Tests;

public sealed class RoutingRuleHealthServiceTests
{
    [Fact]
    public async Task GetOverviewAsync_HasOneDistinctTicket_WithRoutingDecision_NoTerminalOutcome_MatchOneSampleZero()
    {
        await using var db = CreateContext();
        await SeedMinimalTicketGraphAsync(db, ruleId: 55, ticketId: "rule-health-ticket-1");
        db.TicketRoutingDecisions.Add(CreateDecision("rule-health-ticket-1", 55));
        await db.SaveChangesAsync();

        var learning = new CortexLearningService(db, new MemoryCache(new MemoryCacheOptions()), NullLogger<CortexLearningService>.Instance);

        var rulesMock = new Mock<ITicketRoutingRuleService>(MockBehavior.Strict);
        rulesMock.Setup(s => s.GetAllAsync()).ReturnsAsync(new List<TicketRoutingRule>
        {
            new()
            {
                Id = 55,
                RulePriority = 0,
                TitleContains = "test",
                IsEnabled = true,
            },
        });

        var health = new RoutingRuleHealthService(db, learning, rulesMock.Object);
        var overview = await health.GetOverviewAsync(CancellationToken.None);

        var row = Assert.Single(overview.Rules);
        Assert.Equal(55, row.RuleId);
        Assert.Equal(1, row.MatchCount);
        Assert.Equal(0, row.SampleSize);
        Assert.NotNull(row.LastMatchedAtUtc);
        Assert.Equal(RoutingRuleHealthService.HealthInsufficientData, row.HealthStatus);
    }

    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"routing-rule-health-{Guid.NewGuid():N}")
            .Options;
        return new CortexDbContext(options);
    }

    private static async Task SeedMinimalTicketGraphAsync(CortexDbContext db, int ruleId, string ticketId)
    {
        db.Users.Add(new User
        {
            Id = 9001,
            Email = "u@example.com",
            DisplayName = "U",
            Role = Auth0Roles.Admin,
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
        });
        db.TicketBoardDefinitions.Add(new TicketBoardDefinition
        {
            Id = 201,
            Name = "Board",
            IsEnabled = true,
            RequiresStoryPoints = false,
        });
        db.TicketRoutingRules.Add(new TicketRoutingRule
        {
            Id = ruleId,
            RulePriority = 5,
            IsEnabled = true,
        });

        db.Tickets.Add(new Ticket
        {
            Id = ticketId,
            Title = "Title",
            Description = "D",
            BoardId = 201,
            ApprovalStatus = ApprovalStatus.Approved,
            CreatedBy = 9001,
            CreatedDate = DateTime.UtcNow,
            LastModifiedBy = 9001,
        });

        await db.SaveChangesAsync();
    }

    private static TicketRoutingDecision CreateDecision(string ticketId, int matchedRuleId) =>
        new()
        {
            TicketId = ticketId,
            MatchedRuleId = matchedRuleId,
            OutcomeType = RoutingOutcomeType.RuleMatch,
            ConfidenceLevel = RoutingConfidenceLevel.High,
            TieBreakKey = "tie",
            ExplanationJson = "{}",
            ExplanationText = "Matched",
            CreatedDateUtc = new DateTime(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc),
        };
}
