using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Cortex.API.Services.Integrations;
using Cortex.API.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Tests;

public class CreateTicketFromExternalItemTests
{
    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"ext-ticket-{Guid.NewGuid():N}")
            .Options;
        return new CortexDbContext(options);
    }

    private static void SeedBoard(CortexDbContext ctx)
    {
        ctx.TicketBoardDefinitions.Add(
            new TicketBoardDefinition
            {
                Name = $"B-{Guid.NewGuid():N}",
                Description = "Test",
                RequiresStoryPoints = false,
                IsEnabled = true,
                CreatedDateUtc = DateTime.UtcNow,
            });
    }

    [Fact]
    public async Task CreateTicketFromExternalItem_MissingItem_ReturnsNull()
    {
        await using var ctx = CreateContext();
        var fake = new CapturingFakeTicketCreationApplicationService();
        var service = IntegrationServiceTestFactory.Create(ctx, ticketCreation: fake);

        var result = await service.CreateTicketFromExternalItemAsync(999, new CreateTicketFromExternalItemRequest());

        Assert.Null(result);
        Assert.Empty(fake.CapturedRequests);
    }

    [Fact]
    public async Task CreateTicketFromExternalItem_AlreadyLinked_Throws()
    {
        await using var ctx = CreateContext();
        SeedBoard(ctx);
        await ctx.SaveChangesAsync();
        var boardId = await ctx.TicketBoardDefinitions.Select(b => b.Id).FirstAsync();

        var user = new User
        {
            DisplayName = "U",
            Email = "u@t",
            Role = Auth0Roles.Admin,
            CreatedDate = DateTime.UtcNow,
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();

        ctx.Tickets.Add(
            new Ticket
            {
                Id = "EXIST-1",
                Title = "Linked",
                Description = "D",
                BoardId = boardId,
                Priority = "Medium",
                Status = "New",
                ApprovalStatus = ApprovalStatus.Approved,
                RowVersion = [],
                CreatedBy = user.Id,
                CreatedDate = DateTime.UtcNow,
                LastModifiedBy = user.Id,
            });
        await ctx.SaveChangesAsync();

        var integration = IntegrationServiceTestFactory.Create(ctx);
        var source = await CreateSourceAsync(integration);
        var item = await integration.ManualUpsertWorkItemAsync(
            source!.Id,
            new ManualUpsertExternalWorkItemRequest
            {
                ExternalItemId = "x",
                Title = "T",
                CortexTicketId = "EXIST-1",
            });

        var fake = new CapturingFakeTicketCreationApplicationService();
        var service = IntegrationServiceTestFactory.Create(ctx, ticketCreation: fake);

        await Assert.ThrowsAsync<ExternalWorkItemAlreadyLinkedException>(() =>
            service.CreateTicketFromExternalItemAsync(item!.Id, new CreateTicketFromExternalItemRequest()));
        Assert.Empty(fake.CapturedRequests);
    }

    [Fact]
    public async Task CreateTicketFromExternalItem_NoBoard_ThrowsIntegrationApi()
    {
        await using var ctx = CreateContext();
        var integration = IntegrationServiceTestFactory.Create(ctx);
        var source = await CreateSourceAsync(integration);
        var item = await integration.ManualUpsertWorkItemAsync(
            source!.Id,
            new ManualUpsertExternalWorkItemRequest { ExternalItemId = "a", Title = "T" });

        var ex = await Assert.ThrowsAsync<IntegrationApiException>(() =>
            integration.CreateTicketFromExternalItemAsync(item!.Id, new CreateTicketFromExternalItemRequest()));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task CreateTicketFromExternalItem_UsesDefaultBoardMapping_AndLinksItem()
    {
        await using var ctx = CreateContext();
        SeedBoard(ctx);
        await ctx.SaveChangesAsync();
        var boardId = await ctx.TicketBoardDefinitions.Select(b => b.Id).FirstAsync();

        var integration = IntegrationServiceTestFactory.Create(ctx);
        var source = await CreateSourceAsync(integration);
        await integration.ReplaceBoardMappingsAsync(
            source!.Id,
            [
                new ExternalBoardMappingItemRequest
                {
                    BoardId = boardId,
                    MappingMode = ExternalBoardMappingMode.ReferenceOnly,
                    IsDefault = true,
                },
            ]);

        var item = await integration.ManualUpsertWorkItemAsync(
            source.Id,
            new ManualUpsertExternalWorkItemRequest
            {
                ExternalItemId = "sp-1",
                Title = "From external",
                Description = "Body text",
                Priority = "High",
                RawJson = """{"secret":"SHOULD_NOT_APPEAR"}""",
            });

        var fake = new CapturingFakeTicketCreationApplicationService();
        var service = IntegrationServiceTestFactory.Create(ctx, ticketCreation: fake);

        var result = await service.CreateTicketFromExternalItemAsync(item!.Id, new CreateTicketFromExternalItemRequest());

        Assert.NotNull(result);
        Assert.Equal("T-TEST-1", result!.CortexTicketId);
        Assert.Equal(ApprovalStatus.PendingApproval, result.ApprovalStatus);

        var row = await ctx.ExternalWorkItems.SingleAsync();
        Assert.Equal("T-TEST-1", row.CortexTicketId);

        var req = Assert.Single(fake.CapturedRequests);
        Assert.Equal(boardId, req.BoardId);
        Assert.Equal("High", req.Priority);
        Assert.Contains("External source context:", req.Description, StringComparison.Ordinal);
        Assert.Contains("Body text", req.Description, StringComparison.Ordinal);
        Assert.DoesNotContain("SHOULD_NOT_APPEAR", req.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateTicketFromExternalItem_InvalidExternalPriority_FallsBackToMedium()
    {
        await using var ctx = CreateContext();
        SeedBoard(ctx);
        await ctx.SaveChangesAsync();
        var boardId = await ctx.TicketBoardDefinitions.Select(b => b.Id).FirstAsync();

        var integration = IntegrationServiceTestFactory.Create(ctx);
        var source = await CreateSourceAsync(integration);
        await integration.ReplaceBoardMappingsAsync(
            source!.Id,
            [
                new ExternalBoardMappingItemRequest
                {
                    BoardId = boardId,
                    MappingMode = ExternalBoardMappingMode.ReferenceOnly,
                    IsDefault = true,
                },
            ]);

        var item = await integration.ManualUpsertWorkItemAsync(
            source.Id,
            new ManualUpsertExternalWorkItemRequest
            {
                ExternalItemId = "z",
                Title = "T",
                Priority = "not-a-real-priority",
            });

        var fake = new CapturingFakeTicketCreationApplicationService();
        var service = IntegrationServiceTestFactory.Create(ctx, ticketCreation: fake);

        await service.CreateTicketFromExternalItemAsync(item!.Id, new CreateTicketFromExternalItemRequest());

        Assert.Equal("Medium", Assert.Single(fake.CapturedRequests).Priority);
    }

    [Fact]
    public async Task CreateTicketFromExternalItem_WhenTicketCreationFails_DoesNotLink()
    {
        await using var ctx = CreateContext();
        SeedBoard(ctx);
        await ctx.SaveChangesAsync();
        var boardId = await ctx.TicketBoardDefinitions.Select(b => b.Id).FirstAsync();

        var setup = IntegrationServiceTestFactory.Create(ctx);
        var source = await CreateSourceAsync(setup);
        await setup.ReplaceBoardMappingsAsync(
            source!.Id,
            [
                new ExternalBoardMappingItemRequest
                {
                    BoardId = boardId,
                    MappingMode = ExternalBoardMappingMode.ReferenceOnly,
                    IsDefault = true,
                },
            ]);

        var item = await setup.ManualUpsertWorkItemAsync(
            source.Id,
            new ManualUpsertExternalWorkItemRequest { ExternalItemId = "fail", Title = "T" });

        var fake = new CapturingFakeTicketCreationApplicationService
        {
            ThrowOnCreate = new InvalidOperationException("simulated failure"),
        };
        var service = IntegrationServiceTestFactory.Create(ctx, ticketCreation: fake);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateTicketFromExternalItemAsync(item!.Id, new CreateTicketFromExternalItemRequest()));

        var row = await ctx.ExternalWorkItems.AsNoTracking().SingleAsync();
        Assert.Null(row.CortexTicketId);
    }

    private static async Task<ExternalWorkSourceResponse?> CreateSourceAsync(IExternalIntegrationService service)
    {
        var connection = await service.CreateConnectionAsync(
            new CreateIntegrationConnectionRequest
            {
                Provider = IntegrationProvider.SharePoint,
                DisplayName = "Conn",
                TenantId = IntegrationConnectionTestDefaults.SharePointTenantId,
            });

        return await service.CreateSourceAsync(
            connection.Id,
            new CreateExternalWorkSourceRequest
            {
                Provider = IntegrationProvider.SharePoint,
                SourceType = ExternalSourceType.SharePointList,
                ExternalSourceId = $"list-{Guid.NewGuid():N}",
                Name = "Issues",
            });
    }
}
