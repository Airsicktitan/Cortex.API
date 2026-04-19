using Cortex.API.Data.Repositories;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Tests;

public class TicketRepositoryMySubmissionsTests
{
    [Fact]
    public async Task GetTicketByUserAsync_ReturnsCreatorAndOwnerMatchesAcrossIdentityFallbacks()
    {
        await using var context = CreateContext();
        SeedBoard(context);

        var currentUser = CreateUser(
            id: 100,
            email: "current@example.com",
            displayName: "Current User",
            auth0Id: "auth0|current");
        var auth0LinkedCreator = CreateUser(
            id: 101,
            email: "legacy-auth0@example.com",
            displayName: "Legacy Auth0",
            auth0Id: "auth0|current");
        var emailLinkedCreator = CreateUser(
            id: 102,
            email: "current@example.com",
            displayName: "Legacy Email");
        var unrelatedUser = CreateUser(
            id: 200,
            email: "other@example.com",
            displayName: "Other User",
            auth0Id: "auth0|other");

        context.Users.AddRange(currentUser, auth0LinkedCreator, emailLinkedCreator, unrelatedUser);
        context.Tickets.AddRange(
            CreateTicket("T-1001", 100, ApprovalStatus.PendingApproval, createdDate: new DateTime(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc)),
            CreateTicket("T-1002", 101, ApprovalStatus.Approved, createdDate: new DateTime(2026, 4, 18, 12, 5, 0, DateTimeKind.Utc)),
            CreateTicket("T-1003", 102, ApprovalStatus.Rejected, createdDate: new DateTime(2026, 4, 18, 12, 10, 0, DateTimeKind.Utc)),
            CreateTicket("T-1004", 200, ApprovalStatus.NeedsMoreInfo, synitiOwner: "user:100", createdDate: new DateTime(2026, 4, 18, 12, 15, 0, DateTimeKind.Utc)),
            CreateTicket("T-1005", 200, ApprovalStatus.Approved, businessOwner: "current@example.com", createdDate: new DateTime(2026, 4, 18, 12, 20, 0, DateTimeKind.Utc)),
            CreateTicket("T-1006", 200, ApprovalStatus.Approved, synitiOwner: "someone@example.com", createdDate: new DateTime(2026, 4, 18, 12, 25, 0, DateTimeKind.Utc)));
        await context.SaveChangesAsync();

        var repository = new TicketRepository(context);

        var results = (await repository.GetTicketByUserAsync(currentUser))
            .OrderBy(ticket => ticket.Id)
            .ToList();

        Assert.Equal(
            new[] { "T-1001", "T-1002", "T-1003", "T-1004", "T-1005" },
            results.Select(ticket => ticket.Id).ToArray());
        Assert.Equal(
            new[]
            {
                ApprovalStatus.PendingApproval,
                ApprovalStatus.Approved,
                ApprovalStatus.Rejected,
                ApprovalStatus.NeedsMoreInfo,
                ApprovalStatus.Approved,
            },
            results.Select(ticket => ticket.ApprovalStatus).ToArray());
    }

    [Fact]
    public async Task TicketResponseMapping_IncludesCreatorIdentityMetadata()
    {
        await using var context = CreateContext();
        SeedBoard(context);

        var creator = CreateUser(
            id: 300,
            email: "creator@example.com",
            displayName: "Creator",
            auth0Id: "auth0|creator");
        context.Users.Add(creator);
        await context.SaveChangesAsync();

        var ticket = CreateTicket(
            "T-2001",
            creator.Id,
            ApprovalStatus.Approved,
            createdDate: new DateTime(2026, 4, 18, 13, 0, 0, DateTimeKind.Utc));
        var mappingContext = await new ResponseMappingContextFactory(context)
            .CreateAsync([creator.Id], null, [ticket.BoardId]);

        var response = ticket.ToResponse(new Dictionary<string, SlaConfiguration>(), mappingContext);

        Assert.Equal(creator.Email, response.CreatedByEmail);
        Assert.Equal(creator.Auth0Id, response.CreatedByAuth0Id);
    }

    [Fact]
    public async Task TicketResponseMapping_IncludesPersistedApprovalTriagePreview()
    {
        await using var context = CreateContext();
        SeedBoard(context);

        var creator = CreateUser(
            id: 301,
            email: "creator@example.com",
            displayName: "Creator",
            auth0Id: "auth0|creator");
        context.Users.Add(creator);
        await context.SaveChangesAsync();

        var ticket = CreateTicket(
            "T-2002",
            creator.Id,
            ApprovalStatus.PendingApproval,
            createdDate: new DateTime(2026, 4, 19, 13, 0, 0, DateTimeKind.Utc));
        ticket.AiTriageSummary = "Confirm the request scope and needed approval outcome.";
        ticket.AiTriageSuggestedPriority = "High";
        ticket.AiTriagePriorityReason = "The intake blocker affects multiple reviewers.";
        ticket.AiTriageMissingDetailsJson =
            """["Confirm the affected queue.","Identify the approval owner."]""";
        ticket.AiTriagePotentialSlaRisk = "High";
        ticket.AiTriageSlaRiskReason =
            "Broad impact is stated while acceptance criteria and systems-in-scope stay undefined.";

        var mappingContext = await new ResponseMappingContextFactory(context)
            .CreateAsync([creator.Id], null, [ticket.BoardId]);

        var response = ticket.ToResponse(new Dictionary<string, SlaConfiguration>(), mappingContext);

        Assert.NotNull(response.ApprovalTriagePreview);
        Assert.Equal(ticket.AiTriageSummary, response.ApprovalTriagePreview!.Summary);
        Assert.Equal(ticket.AiTriageSuggestedPriority, response.ApprovalTriagePreview.SuggestedPriority);
        Assert.Equal(ticket.AiTriagePriorityReason, response.ApprovalTriagePreview.PriorityReason);
        Assert.Equal(
            new[] { "Confirm the affected queue.", "Identify the approval owner." },
            response.ApprovalTriagePreview.MissingDetailHints);
        Assert.Equal("High", response.ApprovalTriagePreview.PotentialSlaRisk);
        Assert.Equal(
            "Broad impact is stated while acceptance criteria and systems-in-scope stay undefined.",
            response.ApprovalTriagePreview.SlaRiskReason);
    }

    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new CortexDbContext(options);
    }

    private static void SeedBoard(CortexDbContext context)
    {
        context.TicketBoardDefinitions.Add(new TicketBoardDefinition
        {
            Id = 1,
            Name = "Ticket",
            IsEnabled = true,
            RequiresStoryPoints = false,
        });
    }

    private static User CreateUser(
        int id,
        string email,
        string displayName,
        string? auth0Id = null)
    {
        return new User
        {
            Id = id,
            Email = email,
            DisplayName = displayName,
            Auth0Id = auth0Id,
            Role = Auth0Roles.User,
            CreatedDate = new DateTime(2026, 4, 18, 11, 0, 0, DateTimeKind.Utc),
        };
    }

    private static Ticket CreateTicket(
        string id,
        int createdBy,
        ApprovalStatus approvalStatus,
        string? synitiOwner = null,
        string? businessOwner = null,
        DateTime? createdDate = null)
    {
        return new Ticket
        {
            Id = id,
            Title = $"Ticket {id}",
            Description = "Test ticket",
            Status = "New",
            ApprovalStatus = approvalStatus,
            Priority = "Medium",
            BoardId = 1,
            CreatedBy = createdBy,
            CreatedDate = createdDate ?? new DateTime(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc),
            LastModifiedBy = createdBy,
            LastModifiedDate = createdDate ?? new DateTime(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc),
            SynitiOwner = synitiOwner,
            BusinessOwner = businessOwner,
        };
    }
}
