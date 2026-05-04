using Cortex.API.Database;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.API.Tests;

public sealed class CortexLearningRoutingRuleEffectivenessCachingTests
{
    [Fact]
    public async Task GetRoutingRuleEffectivenessAsync_CachesEmptyThen_StaleUntilBypassCacheTrue()
    {
        await using var db = CreateContext();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var svc = new CortexLearningService(db, cache, NullLogger<CortexLearningService>.Instance);

        db.TicketRoutingRules.Add(new TicketRoutingRule { Id = 77, RulePriority = 0, IsEnabled = true });
        await db.SaveChangesAsync();

        var beforeDecisions = await svc.GetRoutingRuleEffectivenessAsync(77, CancellationToken.None);
        Assert.Equal(0, beforeDecisions.TotalDecisions);

        await SeedMinimalTicketWithDecision(db, matchedRuleId: 77);

        var staleWithoutBypass = await svc.GetRoutingRuleEffectivenessAsync(77, CancellationToken.None);
        Assert.Equal(
            0,
            staleWithoutBypass.TotalDecisions);

        var freshWithBypass =
            await svc.GetRoutingRuleEffectivenessAsync(77, CancellationToken.None, bypassCache: true);
        Assert.Equal(1, freshWithBypass.TotalDecisions);
        Assert.Equal(0, freshWithBypass.OutcomeSampleCount);
    }

    private static async Task SeedMinimalTicketWithDecision(CortexDbContext db, int matchedRuleId)
    {
        db.Users.Add(new User
        {
            Id = 9002,
            Email = "u2@example.com",
            DisplayName = "U2",
            Role = Auth0Roles.Admin,
            IsActive = true,
            CreatedDate = DateTime.UtcNow,
        });
        db.TicketBoardDefinitions.Add(new TicketBoardDefinition
        {
            Id = 202,
            Name = "B",
            IsEnabled = true,
            RequiresStoryPoints = false,
        });

        db.Tickets.Add(new Ticket
        {
            Id = "cache-test-ticket",
            Title = "T",
            Description = "d",
            BoardId = 202,
            ApprovalStatus = ApprovalStatus.Approved,
            CreatedBy = 9002,
            CreatedDate = DateTime.UtcNow,
            LastModifiedBy = 9002,
        });

        db.TicketRoutingDecisions.Add(new TicketRoutingDecision
        {
            TicketId = "cache-test-ticket",
            MatchedRuleId = matchedRuleId,
            OutcomeType = RoutingOutcomeType.RuleMatch,
            ConfidenceLevel = RoutingConfidenceLevel.High,
            TieBreakKey = "tie",
            ExplanationJson = "{}",
            ExplanationText = "ok",
            CreatedDateUtc = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
    }

    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"learning-rule-eff-cache-{Guid.NewGuid():N}")
            .Options;

        return new CortexDbContext(options);
    }
}
