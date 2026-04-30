using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Cortex.API.Services.Integrations;
using Cortex.API.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Tests;

public class ExternalIntegrationServiceTests
{
    private static ExternalIntegrationService CreateIntegrationService(
        CortexDbContext context,
        FakeSharePointGraphClient? graph = null)
    {
        graph ??= new FakeSharePointGraphClient();
        return IntegrationServiceTestFactory.Create(context, graph);
    }

    [Fact]
    public async Task CreateConnection_PersistsIntegrationConnection()
    {
        await using var context = CreateContext();
        var service = CreateIntegrationService(context);

        var response = await service.CreateConnectionAsync(new CreateIntegrationConnectionRequest
        {
            Provider = IntegrationProvider.SharePoint,
            DisplayName = "Corp SharePoint",
            TenantId = "tenant-a",
        });

        Assert.True(response.Id > 0);
        Assert.Equal(IntegrationProvider.SharePoint, response.Provider);
        Assert.Equal("Corp SharePoint", response.DisplayName);
        Assert.Equal("tenant-a", response.TenantId);
        Assert.Equal(IntegrationAuthMode.Manual, response.AuthMode);
        Assert.Equal(IntegrationSyncMode.ReadOnly, response.SyncMode);
        Assert.True(response.IsEnabled);

        var persisted = await context.IntegrationConnections.SingleAsync();
        Assert.Equal(response.Id, persisted.Id);
    }

    [Fact]
    public async Task CreateSource_UnderConnection_PersistsExternalWorkSource()
    {
        await using var context = CreateContext();
        var service = CreateIntegrationService(context);

        var connection = await service.CreateConnectionAsync(new CreateIntegrationConnectionRequest
        {
            Provider = IntegrationProvider.SharePoint,
            DisplayName = "Conn",
        });

        var source = await service.CreateSourceAsync(connection.Id, new CreateExternalWorkSourceRequest
        {
            Provider = IntegrationProvider.SharePoint,
            SourceType = ExternalSourceType.SharePointList,
            ExternalSourceId = "list-guid-1",
            Name = "Escalations",
        });

        Assert.NotNull(source);
        Assert.Equal(connection.Id, source!.IntegrationConnectionId);
        Assert.Equal("Escalations", source.Name);

        var row = await context.ExternalWorkSources.SingleAsync();
        Assert.Equal("list-guid-1", row.ExternalSourceId);
    }

    [Fact]
    public async Task ReplaceFieldMappings_ReplacesMappingSet()
    {
        await using var context = CreateContext();
        var service = CreateIntegrationService(context);

        var connection = await service.CreateConnectionAsync(new CreateIntegrationConnectionRequest
        {
            Provider = IntegrationProvider.SharePoint,
            DisplayName = "Conn",
        });
        var source = await service.CreateSourceAsync(connection.Id, new CreateExternalWorkSourceRequest
        {
            Provider = IntegrationProvider.SharePoint,
            SourceType = ExternalSourceType.SharePointList,
            ExternalSourceId = "src-1",
            Name = "List",
        });

        await service.ReplaceFieldMappingsAsync(source!.Id,
        [
            new ExternalFieldMappingItemRequest
            {
                ExternalFieldName = "Title",
                CortexField = CortexField.Title,
                IsRequired = true,
            },
            new ExternalFieldMappingItemRequest
            {
                ExternalFieldName = "Priority",
                ExternalFieldKey = "priority",
                CortexField = CortexField.Priority,
                TransformHint = "map-high",
            },
        ]);

        var mappings = await context.ExternalFieldMappings.OrderBy(m => m.CortexField).ToListAsync();
        Assert.Equal(2, mappings.Count);
        Assert.Equal(CortexField.Title, mappings[0].CortexField);
        Assert.Equal(CortexField.Priority, mappings[1].CortexField);
        Assert.Equal("map-high", mappings.Single(m => m.CortexField == CortexField.Priority).TransformHint);
    }

    [Fact]
    public async Task ReplaceBoardMappings_StoresBoardLinks()
    {
        await using var context = CreateContext();
        SeedBoard(context);
        await context.SaveChangesAsync();
        var boardId = await context.TicketBoardDefinitions.Select(b => b.Id).FirstAsync();

        var service = CreateIntegrationService(context);
        var connection = await service.CreateConnectionAsync(new CreateIntegrationConnectionRequest
        {
            Provider = IntegrationProvider.Jira,
            DisplayName = "Jira",
        });
        var source = await service.CreateSourceAsync(connection.Id, new CreateExternalWorkSourceRequest
        {
            Provider = IntegrationProvider.Jira,
            SourceType = ExternalSourceType.JiraProject,
            ExternalSourceId = "PROJ-1",
            Name = "Ops",
        });

        await service.ReplaceBoardMappingsAsync(source!.Id,
        [
            new ExternalBoardMappingItemRequest
            {
                BoardId = boardId,
                MappingMode = ExternalBoardMappingMode.ReferenceOnly,
                IsDefault = true,
            },
        ]);

        var maps = await context.ExternalBoardMappings.SingleAsync();
        Assert.Equal(boardId, maps.BoardId);
        Assert.True(maps.IsDefault);
        Assert.Equal(ExternalBoardMappingMode.ReferenceOnly, maps.MappingMode);
    }

    [Fact]
    public async Task ManualUpsert_CreatesExternalWorkItem()
    {
        await using var context = CreateContext();
        var service = CreateIntegrationService(context);
        var source = await CreateSharePointSourceAsync(service);

        var item = await service.ManualUpsertWorkItemAsync(source!.Id, new ManualUpsertExternalWorkItemRequest
        {
            ExternalItemId = "42",
            Title = "Fix portal",
            Status = "Active",
            RawJson = """{"foo":1}""",
        });

        Assert.NotNull(item);
        Assert.Equal("42", item!.ExternalItemId);
        Assert.Equal("Fix portal", item.Title);
        Assert.Null(item.CortexTicketId);

        var row = await context.ExternalWorkItems.SingleAsync();
        Assert.Equal("Fix portal", row.Title);
        Assert.False(row.IsDeleted);
    }

    [Fact]
    public async Task ManualUpsert_SameExternalItemId_UpdatesInsteadOfDuplicating()
    {
        await using var context = CreateContext();
        var service = CreateIntegrationService(context);
        var source = await CreateSharePointSourceAsync(service);

        await service.ManualUpsertWorkItemAsync(source!.Id, new ManualUpsertExternalWorkItemRequest
        {
            ExternalItemId = "same",
            Title = "First",
        });
        await service.ManualUpsertWorkItemAsync(source.Id, new ManualUpsertExternalWorkItemRequest
        {
            ExternalItemId = "same",
            Title = "Second",
        });

        Assert.Equal(1, await context.ExternalWorkItems.CountAsync());
        var row = await context.ExternalWorkItems.SingleAsync();
        Assert.Equal("Second", row.Title);
    }

    [Fact]
    public async Task ManualUpsert_AllowsNullCortexTicketId()
    {
        await using var context = CreateContext();
        var service = CreateIntegrationService(context);
        var source = await CreateSharePointSourceAsync(service);

        var item = await service.ManualUpsertWorkItemAsync(source!.Id, new ManualUpsertExternalWorkItemRequest
        {
            ExternalItemId = "x",
            Title = "No ticket",
        });

        Assert.Null(item!.CortexTicketId);
    }

    [Fact]
    public async Task ManualUpsert_CanLinkExistingCortexTicket()
    {
        await using var context = CreateContext();
        SeedBoard(context);
        var user = new User
        {
            DisplayName = "U",
            Email = "u@test",
            Role = Auth0Roles.Admin,
            CreatedDate = DateTime.UtcNow,
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        var boardId = await context.TicketBoardDefinitions.Select(b => b.Id).FirstAsync();
        var ticket = new Ticket
        {
            Id = "EXT-LINK-1",
            Title = "Cortex ticket",
            Description = "D",
            Status = "New",
            Priority = "Medium",
            BoardId = boardId,
            CreatedBy = user.Id,
            LastModifiedBy = user.Id,
            CreatedDate = DateTime.UtcNow,
        };
        context.Tickets.Add(ticket);
        await context.SaveChangesAsync();

        var service = CreateIntegrationService(context);
        var source = await CreateSharePointSourceAsync(service);

        var item = await service.ManualUpsertWorkItemAsync(source!.Id, new ManualUpsertExternalWorkItemRequest
        {
            ExternalItemId = "linked",
            Title = "External",
            CortexTicketId = ticket.Id,
        });

        Assert.Equal(ticket.Id, item!.CortexTicketId);
        var row = await context.ExternalWorkItems.SingleAsync();
        Assert.Equal(ticket.Id, row.CortexTicketId);
    }

    private static void SeedBoard(CortexDbContext context)
    {
        context.TicketBoardDefinitions.Add(new TicketBoardDefinition
        {
            Name = $"TestBoard-{Guid.NewGuid():N}",
            Description = "Test",
            RequiresStoryPoints = false,
            IsEnabled = true,
            CreatedDateUtc = DateTime.UtcNow,
        });
    }

    private static async Task<ExternalWorkSourceResponse?> CreateSharePointSourceAsync(IExternalIntegrationService service)
    {
        var connection = await service.CreateConnectionAsync(new CreateIntegrationConnectionRequest
        {
            Provider = IntegrationProvider.SharePoint,
            DisplayName = "SPO",
        });
        return await service.CreateSourceAsync(connection.Id, new CreateExternalWorkSourceRequest
        {
            Provider = IntegrationProvider.SharePoint,
            SourceType = ExternalSourceType.SharePointList,
            ExternalSourceId = $"spo-{Guid.NewGuid():N}",
            Name = "Issues",
        });
    }

    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"integrations-{Guid.NewGuid():N}")
            .Options;

        return new CortexDbContext(options);
    }
}
