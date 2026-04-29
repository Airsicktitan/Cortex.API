using Cortex.API.Database;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cortex.API.Tests;

public class TicketOutcomeServiceTests
{
    [Fact]
    public async Task MarkReturnedForDetailAsync_CreatesOutcomeAndSetsFlag()
    {
        await using var db = CreateContext();
        var service = CreateService(db);
        var ticket = CreateTicket(
            id: "T-RETURN",
            approvalStatus: ApprovalStatus.NeedsMoreInfo);

        await service.MarkReturnedForDetailAsync(ticket);

        var outcome = Assert.Single(await db.TicketOutcomes.ToListAsync());
        Assert.Equal(ticket.Id, outcome.TicketId);
        Assert.True(outcome.WasReturnedForDetail);
    }

    [Fact]
    public async Task MarkReassignedAsync_OwnerChangeAfterInitialAssignmentSetsFlag()
    {
        await using var db = CreateContext();
        var service = CreateService(db);
        var ticket = CreateTicket(id: "T-REASSIGN", synitiOwner: "Sarah Chen");

        await service.RecordInitialAssignmentAsync(ticket, matchedRuleId: 42);

        ticket.SynitiOwner = "John Rivera";
        await service.MarkReassignedAsync(ticket, previousSynitiOwner: "Sarah Chen");

        var outcome = Assert.Single(await db.TicketOutcomes.ToListAsync());
        Assert.True(outcome.WasReassigned);
        Assert.Equal("Sarah Chen", outcome.AssignedSynitiOwner);
        Assert.Equal("John Rivera", outcome.FinalOwner);
        Assert.Equal(1, await db.TicketOutcomes.CountAsync(o => o.TicketId == ticket.Id));
    }

    [Fact]
    public async Task RecordTerminalAsync_UsesSlaCalculatorAndCapturesBreach()
    {
        await using var db = CreateContext();
        var service = CreateService(db);
        var completedAt = DateTime.UtcNow;
        var ticket = CreateTicket(
            id: "T-SLA",
            status: "Resolved",
            approvalStatus: ApprovalStatus.Approved,
            approvedAt: completedAt.AddDays(-2),
            lastModifiedDate: completedAt);

        await service.RecordTerminalAsync(ticket);

        var outcome = Assert.Single(await db.TicketOutcomes.ToListAsync());
        Assert.True(outcome.SlaBreached);
        Assert.True(outcome.WasSlaBreached);
        Assert.True(outcome.ReachedTerminalStatus);
    }

    [Fact]
    public async Task MarkRoutingOverriddenAsync_CapturesManualOverride()
    {
        await using var db = CreateContext();
        var service = CreateService(db);

        await service.MarkRoutingOverriddenAsync(
            ticketId: "T-OVERRIDE",
            finalSynitiOwner: "Maria Gomez",
            finalBusinessOwner: "Finance Queue");

        var outcome = Assert.Single(await db.TicketOutcomes.ToListAsync());
        Assert.True(outcome.WasOverridden);
        Assert.True(outcome.WasRoutingOverridden);
        Assert.Equal("Maria Gomez", outcome.FinalOwner);
        Assert.Equal("Finance Queue", outcome.FinalBusinessOwner);
    }

    [Fact]
    public async Task MarkCompletedAsync_SetsFinalOwnerAndCompletedAt()
    {
        await using var db = CreateContext();
        var service = CreateService(db);
        var ticket = CreateTicket(
            id: "T-DONE",
            synitiOwner: "Priya Shah",
            status: "Closed",
            approvalStatus: ApprovalStatus.Approved);

        await service.MarkCompletedAsync(ticket, slaBreached: false, commentCount: 3);

        var outcome = Assert.Single(await db.TicketOutcomes.ToListAsync());
        Assert.Equal("Priya Shah", outcome.FinalOwner);
        Assert.Equal("Priya Shah", outcome.FinalSynitiOwner);
        Assert.True(outcome.CompletedAt.HasValue);
        Assert.True(outcome.CompletedAtUtc.HasValue);
        Assert.True(outcome.ReachedTerminalStatus);
        Assert.Equal(3, outcome.CommentCount);
    }

    [Fact]
    public async Task OutcomeCapture_UpdatesSingleRecordPerTicket()
    {
        await using var db = CreateContext();
        var service = CreateService(db);
        var ticket = CreateTicket(id: "T-ONE", synitiOwner: "Initial Owner");

        await service.RecordInitialAssignmentAsync(ticket, matchedRuleId: 7);
        await service.MarkReturnedForDetailAsync(ticket);
        ticket.SynitiOwner = "Final Owner";
        await service.MarkReassignedAsync(ticket, previousSynitiOwner: "Initial Owner");
        await service.MarkCompletedAsync(ticket, slaBreached: false, commentCount: 1);

        var outcome = Assert.Single(await db.TicketOutcomes.ToListAsync());
        Assert.Equal(ticket.Id, outcome.TicketId);
        Assert.True(outcome.WasReturnedForDetail);
        Assert.True(outcome.WasReassigned);
    }

    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"ticket-outcomes-{Guid.NewGuid():N}")
            .Options;

        return new CortexDbContext(options);
    }

    private static TicketOutcomeService CreateService(CortexDbContext db)
    {
        var slaConfigurationService = new Mock<ISlaConfigurationService>();
        slaConfigurationService
            .Setup(service => service.GetPriorityMapAsync())
            .ReturnsAsync(TicketSlaCalculator.GetDefaultPolicies()
                .ToDictionary(policy => policy.Priority, StringComparer.OrdinalIgnoreCase));

        return new TicketOutcomeService(
            db,
            slaConfigurationService.Object,
            NullLogger<TicketOutcomeService>.Instance);
    }

    private static Ticket CreateTicket(
        string id,
        string synitiOwner = "Sarah Chen",
        string? businessOwner = "Finance Queue",
        string status = "New",
        ApprovalStatus approvalStatus = ApprovalStatus.PendingApproval,
        DateTime? approvedAt = null,
        DateTime? lastModifiedDate = null)
    {
        return new Ticket
        {
            Id = id,
            Title = "Outcome capture ticket",
            Description = "Validate outcome capture.",
            Priority = "Medium",
            BoardId = 1,
            SynitiOwner = synitiOwner,
            BusinessOwner = businessOwner,
            Status = status,
            ApprovalStatus = approvalStatus,
            ApprovedAt = approvedAt,
            LastModifiedDate = lastModifiedDate,
            CreatedBy = 1,
            CreatedDate = DateTime.UtcNow.AddDays(-3),
            LastModifiedBy = 1,
        };
    }
}
