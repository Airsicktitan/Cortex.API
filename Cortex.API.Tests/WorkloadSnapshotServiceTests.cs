using Cortex.API.Database;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace Cortex.API.Tests;

public class WorkloadSnapshotServiceTests
{
    [Fact]
    public async Task GetSnapshotsAsync_ComputesWorkloadScore_AndStatusBuckets()
    {
        await using var context = CreateContext();
        context.Users.Add(new User
        {
            Id = 1,
            DisplayName = "owner-a",
            Email = "owner-a@example.com",
            Role = Auth0Roles.Developer,
            IsActive = true,
            IsSynitiOwnerEligible = true
        });
        context.Tickets.AddRange(
            new Ticket
            {
                Id = "T-1",
                Title = "A",
                Description = "A",
                Status = "New",
                ApprovalStatus = ApprovalStatus.Approved,
                Priority = "High",
                SynitiOwner = "owner-a",
                CreatedBy = 1,
                CreatedDate = DateTime.UtcNow
            },
            new Ticket
            {
                Id = "T-2",
                Title = "B",
                Description = "B",
                Status = "New",
                ApprovalStatus = ApprovalStatus.Approved,
                Priority = "Critical",
                SynitiOwner = "owner-a",
                CreatedBy = 1,
                CreatedDate = DateTime.UtcNow.AddHours(-40)
            });
        await context.SaveChangesAsync();

        var slaService = new Mock<ISlaConfigurationService>(MockBehavior.Strict);
        slaService
            .Setup(service => service.GetPriorityMapAsync())
            .ReturnsAsync(new Dictionary<string, SlaConfiguration>(StringComparer.OrdinalIgnoreCase)
            {
                ["High"] = new SlaConfiguration { Priority = "High", TargetHours = 72, WarningHours = 24 },
                ["Critical"] = new SlaConfiguration { Priority = "Critical", TargetHours = 4, WarningHours = 1 }
            });

        var service = new WorkloadSnapshotService(context, slaService.Object);
        var snapshots = await service.GetSnapshotsAsync();

        var snapshot = Assert.Single(snapshots);
        Assert.Equal("user:1", snapshot.UserId);
        Assert.Equal(2, snapshot.ActiveTicketCount);
        Assert.Equal(2, snapshot.HighPriorityCount);
        Assert.Equal(1, snapshot.OverdueTicketCount);
        Assert.Equal(0, snapshot.SlaRiskCount);
        Assert.Equal(0, snapshot.StaleTicketCount);
        Assert.Equal(9m, snapshot.WorkloadScore);
        Assert.Equal("Available", snapshot.Status);
    }

    [Fact]
    public async Task GetSnapshotsAsync_ExcludesResolvedAndClosedTicketsFromActiveCounts()
    {
        await using var context = CreateContext();
        context.Users.Add(new User
        {
            Id = 1,
            DisplayName = "owner-a",
            Email = "owner-a@example.com",
            Role = Auth0Roles.Developer,
            IsActive = true,
            IsSynitiOwnerEligible = true
        });
        context.Tickets.AddRange(
            new Ticket
            {
                Id = "T-1",
                Title = "A",
                Description = "A",
                Status = "New",
                ApprovalStatus = ApprovalStatus.Approved,
                Priority = "High",
                SynitiOwner = "owner-a",
                CreatedBy = 1,
                CreatedDate = DateTime.UtcNow
            },
            new Ticket
            {
                Id = "T-2",
                Title = "B",
                Description = "B",
                Status = "Resolved",
                ApprovalStatus = ApprovalStatus.Approved,
                Priority = "Critical",
                SynitiOwner = "owner-a",
                CreatedBy = 1,
                CreatedDate = DateTime.UtcNow.AddHours(-40)
            },
            new Ticket
            {
                Id = "T-3",
                Title = "C",
                Description = "C",
                Status = "Closed",
                ApprovalStatus = ApprovalStatus.Approved,
                Priority = "Critical",
                SynitiOwner = "owner-a",
                CreatedBy = 1,
                CreatedDate = DateTime.UtcNow.AddHours(-40)
            });
        await context.SaveChangesAsync();

        var slaService = new Mock<ISlaConfigurationService>(MockBehavior.Strict);
        slaService
            .Setup(service => service.GetPriorityMapAsync())
            .ReturnsAsync(new Dictionary<string, SlaConfiguration>(StringComparer.OrdinalIgnoreCase)
            {
                ["High"] = new SlaConfiguration { Priority = "High", TargetHours = 72, WarningHours = 24 },
                ["Critical"] = new SlaConfiguration { Priority = "Critical", TargetHours = 4, WarningHours = 1 }
            });

        var service = new WorkloadSnapshotService(context, slaService.Object);
        var snapshots = await service.GetSnapshotsAsync();

        var snapshot = Assert.Single(snapshots);
        Assert.Equal("user:1", snapshot.UserId);
        Assert.Equal(1, snapshot.ActiveTicketCount);
        Assert.Equal(1, snapshot.HighPriorityCount);
    }

    [Fact]
    public async Task GetSnapshotsAsync_GroupsLegacyDisplayEmailAndCanonicalOwnerKeys()
    {
        await using var context = CreateContext();
        context.Users.Add(new User
        {
            Id = 1,
            DisplayName = "Adam Hooper",
            Email = "adamcwhooper@yahoo.com",
            Role = Auth0Roles.Developer,
            IsActive = true,
            IsSynitiOwnerEligible = true
        });
        context.Tickets.AddRange(
            new Ticket
            {
                Id = "T-1",
                Title = "A",
                Description = "A",
                Status = "New",
                ApprovalStatus = ApprovalStatus.Approved,
                Priority = "Low",
                SynitiOwner = "Adam Hooper",
                CreatedBy = 1,
                CreatedDate = DateTime.UtcNow
            },
            new Ticket
            {
                Id = "T-2",
                Title = "B",
                Description = "B",
                Status = "New",
                ApprovalStatus = ApprovalStatus.Approved,
                Priority = "Low",
                SynitiOwner = "adamcwhooper@yahoo.com",
                CreatedBy = 1,
                CreatedDate = DateTime.UtcNow
            },
            new Ticket
            {
                Id = "T-3",
                Title = "C",
                Description = "C",
                Status = "New",
                ApprovalStatus = ApprovalStatus.Approved,
                Priority = "Low",
                SynitiOwner = "user:1",
                CreatedBy = 1,
                CreatedDate = DateTime.UtcNow
            });
        await context.SaveChangesAsync();

        var slaService = new Mock<ISlaConfigurationService>(MockBehavior.Strict);
        slaService
            .Setup(service => service.GetPriorityMapAsync())
            .ReturnsAsync(new Dictionary<string, SlaConfiguration>(StringComparer.OrdinalIgnoreCase));

        var service = new WorkloadSnapshotService(context, slaService.Object);
        var snapshots = await service.GetSnapshotsAsync();

        var snapshot = Assert.Single(snapshots);
        Assert.Equal("user:1", snapshot.UserId);
        Assert.Equal(3, snapshot.ActiveTicketCount);
    }

    [Fact]
    public async Task GetSnapshotsAsync_IncludesZeroTicketEligibleUsers_WithZeroWorkloadScore()
    {
        await using var context = CreateContext();
        context.Users.Add(new User
        {
            Id = 1,
            DisplayName = "available-owner",
            Email = "available-owner@example.com",
            Role = Auth0Roles.Developer,
            IsActive = true,
            IsSynitiOwnerEligible = true
        });
        await context.SaveChangesAsync();

        var slaService = new Mock<ISlaConfigurationService>(MockBehavior.Strict);
        slaService
            .Setup(service => service.GetPriorityMapAsync())
            .ReturnsAsync(new Dictionary<string, SlaConfiguration>(StringComparer.OrdinalIgnoreCase));

        var service = new WorkloadSnapshotService(context, slaService.Object);
        var snapshots = await service.GetSnapshotsAsync();

        var snapshot = Assert.Single(snapshots);
        Assert.Equal("user:1", snapshot.UserId);
        Assert.Equal(0, snapshot.ActiveTicketCount);
        Assert.Equal(0m, snapshot.WorkloadScore);
        Assert.Equal("Available", snapshot.Status);
    }

    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"workload-snapshot-{Guid.NewGuid():N}")
            .Options;

        return new CortexDbContext(options);
    }
}
