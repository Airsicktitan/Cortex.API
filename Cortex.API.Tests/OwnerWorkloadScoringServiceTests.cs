using Cortex.API.Database;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Cortex.API.Tests;

public class OwnerWorkloadScoringServiceTests
{
    [Fact]
    public async Task GetScoresAsync_CalculatesStarterWorkloadScore_AndExcludesCurrentTicket()
    {
        await using var context = CreateContext();
        var now = DateTime.UtcNow;

        context.Tickets.AddRange(
            CreateTicket("T-100", "owner-a", "owner-a", "Low", ApprovalStatus.Approved, "New", now),
            CreateTicket("T-101", "owner-a", null, "High", ApprovalStatus.Approved, "New", now),
            CreateTicket("T-102", "owner-a", null, "Medium", ApprovalStatus.Approved, "New", now.AddHours(-1)),
            CreateTicket("T-103", "owner-a", null, "Medium", ApprovalStatus.Approved, "New", now.AddHours(-5)),
            CreateTicket("T-104", "owner-a", null, "Low", ApprovalStatus.Approved, "Resolved", now.AddHours(-2)),
            CreateTicket("T-105", "owner-a", null, "High", ApprovalStatus.PendingApproval, "New", now),
            CreateTicket("T-106", "owner-a", null, "High", ApprovalStatus.Approved, "New", now),
            CreateTicket("T-107", "owner-a", null, "Low", ApprovalStatus.Approved, "New", now));

        context.ArchivedTickets.Add(new ArchivedTicket
        {
            Id = "T-107",
            Title = "Archived",
            Description = "Archived ticket",
            Status = "New",
            Priority = "Low",
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

        var visibilityService = new Mock<ITicketVisibilityService>(MockBehavior.Strict);
        var slaConfigurationService = new Mock<ISlaConfigurationService>(MockBehavior.Strict);
        slaConfigurationService
            .Setup(service => service.GetPriorityMapAsync())
            .ReturnsAsync(new Dictionary<string, SlaConfiguration>
            {
                ["Low"] = new() { Priority = "Low", TargetHours = 24, WarningHours = 8 },
                ["High"] = new() { Priority = "High", TargetHours = 8, WarningHours = 2 },
                ["Medium"] = new() { Priority = "Medium", TargetHours = 4, WarningHours = 4 },
            });

        var service = new OwnerWorkloadScoringService(
            context,
            visibilityService.Object,
            slaConfigurationService.Object);

        var scores = await service.GetScoresAsync(
            ["owner-a"],
            excludeTicketId: "T-106",
            respectCurrentVisibility: false);

        var score = Assert.Single(scores);
        Assert.Equal("owner-a", score.OwnerKey);
        Assert.Equal(4, score.ActiveTicketCount);
        Assert.Equal(1, score.HighPriorityTicketCount);
        Assert.Equal(1, score.AtRiskTicketCount);
        Assert.Equal(1, score.OutsideSlaOpenCount);
        Assert.Equal(2, score.SlaRiskTicketCount);
        Assert.Equal(12, score.WorkloadScore);

        visibilityService.Verify(service => service.GetCurrentVisibilityAsync(), Times.Never);
    }

    [Fact]
    public async Task GetScoresAsync_MatchesDirectoryAliases_ForSameOwner()
    {
        await using var context = CreateContext();
        var now = DateTime.UtcNow;

        context.Users.Add(new User
        {
            Id = 5,
            DisplayName = "Owner Alias",
            NickName = "Alias",
            Email = "owner.alias@example.com",
            IsActive = true,
        });
        context.Tickets.AddRange(
            CreateTicket("T-200", "user:5", null, "Low", ApprovalStatus.Approved, "New", now),
            CreateTicket("T-201", "Owner Alias", null, "High", ApprovalStatus.Approved, "New", now),
            CreateTicket("T-202", null, "Alias", "Medium", ApprovalStatus.Approved, "New", now));
        await context.SaveChangesAsync();

        var visibilityService = new Mock<ITicketVisibilityService>(MockBehavior.Strict);
        var slaConfigurationService = new Mock<ISlaConfigurationService>(MockBehavior.Strict);
        slaConfigurationService
            .Setup(service => service.GetPriorityMapAsync())
            .ReturnsAsync(new Dictionary<string, SlaConfiguration>
            {
                ["Low"] = new() { Priority = "Low", TargetHours = 24, WarningHours = 8 },
                ["High"] = new() { Priority = "High", TargetHours = 8, WarningHours = 2 },
                ["Medium"] = new() { Priority = "Medium", TargetHours = 24, WarningHours = 8 },
            });

        var service = new OwnerWorkloadScoringService(
            context,
            visibilityService.Object,
            slaConfigurationService.Object);

        var scores = await service.GetScoresAsync(
            ["owner.alias@example.com"],
            respectCurrentVisibility: false);

        var score = Assert.Single(scores);
        Assert.Equal("owner.alias@example.com", score.OwnerKey);
        Assert.Equal(3, score.ActiveTicketCount);
        Assert.Equal(1, score.HighPriorityTicketCount);

        visibilityService.Verify(service => service.GetCurrentVisibilityAsync(), Times.Never);
    }

    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"owner-workload-{Guid.NewGuid():N}")
            .Options;

        return new CortexDbContext(options);
    }

    private static Ticket CreateTicket(
        string id,
        string? synitiOwner,
        string? businessOwner,
        string priority,
        ApprovalStatus approvalStatus,
        string status,
        DateTime createdDateUtc)
    {
        return new Ticket
        {
            Id = id,
            Title = $"Ticket {id}",
            Description = "Workload test ticket",
            Status = status,
            ApprovalStatus = approvalStatus,
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
