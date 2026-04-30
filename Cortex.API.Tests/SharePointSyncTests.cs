using System.Text.Json;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Cortex.API.Services.Integrations;
using Cortex.API.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Tests;

public class SharePointSyncTests
{
    private const string ValidListUrl =
        "https://fabrikam.sharepoint.com/sites/support/Lists/Tickets";

    private static ExternalIntegrationService CreateService(CortexDbContext ctx, FakeSharePointGraphClient graph) =>
        IntegrationServiceTestFactory.Create(ctx, graph);

    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"spo-sync-{Guid.NewGuid():N}")
            .Options;
        return new CortexDbContext(options);
    }

    private static async Task<(ExternalIntegrationService Service, int ConnectionId, int SourceId, FakeSharePointGraphClient Graph)>
        SeedSharePointSourceAsync(CortexDbContext ctx, Action<FakeSharePointGraphClient>? configure = null)
    {
        var graph = new FakeSharePointGraphClient();
        configure?.Invoke(graph);
        var service = CreateService(ctx, graph);
        var conn = await service.CreateConnectionAsync(new CreateIntegrationConnectionRequest
        {
            Provider = IntegrationProvider.SharePoint,
            DisplayName = "Conn",
            IsEnabled = true,
        });
        var source = await service.CreateSourceAsync(conn.Id, new CreateExternalWorkSourceRequest
        {
            Provider = IntegrationProvider.SharePoint,
            SourceType = ExternalSourceType.SharePointList,
            ExternalSourceId = "list-guid",
            Name = "Issues",
            ExternalUrl = ValidListUrl,
            IsEnabled = true,
        });

        await service.ReplaceFieldMappingsAsync(source!.Id,
        [
            new ExternalFieldMappingItemRequest
            {
                ExternalFieldName = "Title",
                CortexField = CortexField.Title,
            },
            new ExternalFieldMappingItemRequest
            {
                ExternalFieldName = "Priority",
                CortexField = CortexField.Priority,
            },
        ]);

        return (service, conn.Id, source.Id, graph);
    }

    private static JsonElement ListItem(string id, string title, string? priority = null, string? webUrl = null)
    {
        var fields = new Dictionary<string, string?> { ["Title"] = title };
        if (priority != null)
        {
            fields["Priority"] = priority;
        }

        var o = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["lastModifiedDateTime"] = "2024-01-15T10:00:00Z",
            ["fields"] = fields,
        };
        if (webUrl != null)
        {
            o["webUrl"] = webUrl;
        }

        return FakeSharePointGraphClient.ParseJson(JsonSerializer.Serialize(o));
    }

    [Fact]
    public async Task Sync_MissingSource_ReturnsNull()
    {
        await using var ctx = CreateContext();
        var graph = new FakeSharePointGraphClient();
        var service = CreateService(ctx, graph);
        var result = await service.SyncSharePointSourceAsync(99999);
        Assert.Null(result);
    }

    [Fact]
    public async Task Sync_DisabledSource_Throws_BadRequest()
    {
        await using var ctx = CreateContext();
        var (service, _, sourceId, graph) = await SeedSharePointSourceAsync(ctx, g =>
        {
            g.Items = [ListItem("1", "A", "High")];
        });
        await service.SetSourceEnabledAsync(sourceId, false);
        var ex = await Assert.ThrowsAsync<IntegrationApiException>(() => service.SyncSharePointSourceAsync(sourceId));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Sync_DisabledConnection_Throws_BadRequest()
    {
        await using var ctx = CreateContext();
        var (service, connId, sourceId, graph) = await SeedSharePointSourceAsync(ctx, g =>
        {
            g.Items = [ListItem("1", "A")];
        });
        await service.SetConnectionEnabledAsync(connId, false);
        var ex = await Assert.ThrowsAsync<IntegrationApiException>(() => service.SyncSharePointSourceAsync(sourceId));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Sync_NonSharePointSource_Throws()
    {
        await using var ctx = CreateContext();
        var graph = new FakeSharePointGraphClient();
        var service = CreateService(ctx, graph);
        var conn = await service.CreateConnectionAsync(new CreateIntegrationConnectionRequest
        {
            Provider = IntegrationProvider.Jira,
            DisplayName = "Jira",
        });
        var source = await service.CreateSourceAsync(conn.Id, new CreateExternalWorkSourceRequest
        {
            Provider = IntegrationProvider.Jira,
            SourceType = ExternalSourceType.JiraProject,
            ExternalSourceId = "JP",
            Name = "Board",
        });

        var ex = await Assert.ThrowsAsync<IntegrationApiException>(() => service.SyncSharePointSourceAsync(source!.Id));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Sync_InvalidSharePointUrl_Throws()
    {
        await using var ctx = CreateContext();
        var (service, _, sourceId, _) = await SeedSharePointSourceAsync(ctx);
        var source = await ctx.ExternalWorkSources.FirstAsync(s => s.Id == sourceId);
        source.ExternalUrl = "not-a-valid-sharepoint-list-url";
        await ctx.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<IntegrationApiException>(() => service.SyncSharePointSourceAsync(sourceId));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Sync_NormalizesRowsUsingFieldMappings()
    {
        await using var ctx = CreateContext();
        var (service, _, sourceId, graph) = await SeedSharePointSourceAsync(ctx, g =>
        {
            g.Items =
            [
                ListItem("100", "Alpha", "1 - High", "https://fabrikam.sharepoint.com/item100"),
            ];
        });

        var result = await service.SyncSharePointSourceAsync(sourceId);
        Assert.NotNull(result);
        Assert.Equal(1, result!.CreatedCount);
        Assert.Equal(0, result.UpdatedCount);

        var row = await ctx.ExternalWorkItems.SingleAsync();
        Assert.Equal("100", row.ExternalItemId);
        Assert.Equal("Alpha", row.Title);
        Assert.Equal("1 - High", row.Priority);
        Assert.Equal("https://fabrikam.sharepoint.com/item100", row.ExternalUrl);
    }

    [Fact]
    public async Task Sync_CreatesThenUpdates_ExternalWorkItem()
    {
        await using var ctx = CreateContext();
        var (service, _, sourceId, graph) = await SeedSharePointSourceAsync(ctx, g =>
        {
            g.Items = [ListItem("55", "First", "Low")];
        });

        var r1 = await service.SyncSharePointSourceAsync(sourceId);
        Assert.Equal(1, r1!.CreatedCount);

        graph.Items = [ListItem("55", "Second", "High")];
        var r2 = await service.SyncSharePointSourceAsync(sourceId);
        Assert.Equal(0, r2!.CreatedCount);
        Assert.Equal(1, r2.UpdatedCount);

        Assert.Equal(1, await ctx.ExternalWorkItems.CountAsync());
        var row = await ctx.ExternalWorkItems.SingleAsync();
        Assert.Equal("Second", row.Title);
        Assert.Equal("High", row.Priority);
    }

    [Fact]
    public async Task Sync_DoesNotCreateCortexTickets()
    {
        await using var ctx = CreateContext();
        var (service, _, sourceId, graph) = await SeedSharePointSourceAsync(ctx, g =>
        {
            g.Items =
            [
                ListItem("1", "One"),
                ListItem("2", "Two"),
            ];
        });

        var ticketCountBefore = await ctx.Tickets.CountAsync();
        _ = await service.SyncSharePointSourceAsync(sourceId);
        var ticketCountAfter = await ctx.Tickets.CountAsync();
        Assert.Equal(ticketCountBefore, ticketCountAfter);
        Assert.Equal(2, await ctx.ExternalWorkItems.CountAsync());
    }

    [Fact]
    public async Task Sync_PreservesExistingCortexTicketId_OnUpdate()
    {
        await using var ctx = CreateContext();
        SeedBoard(ctx);
        var user = new User
        {
            DisplayName = "U",
            Email = "u@test",
            Role = Auth0Roles.Admin,
            CreatedDate = DateTime.UtcNow,
        };
        ctx.Users.Add(user);
        await ctx.SaveChangesAsync();
        var boardId = await ctx.TicketBoardDefinitions.Select(b => b.Id).FirstAsync();
        var ticket = new Ticket
        {
            Id = "SPO-PRESERVE-1",
            Title = "Linked",
            Description = "D",
            Status = "New",
            Priority = "Medium",
            BoardId = boardId,
            CreatedBy = user.Id,
            LastModifiedBy = user.Id,
            CreatedDate = DateTime.UtcNow,
        };
        ctx.Tickets.Add(ticket);
        await ctx.SaveChangesAsync();

        var (service, _, sourceId, graph) = await SeedSharePointSourceAsync(ctx, g =>
        {
            g.Items = [ListItem("77", "External title", "Low")];
        });

        var link = await service.ManualUpsertWorkItemAsync(sourceId, new ManualUpsertExternalWorkItemRequest
        {
            ExternalItemId = "77",
            Title = "Old ext title",
            CortexTicketId = ticket.Id,
        });
        Assert.Equal(ticket.Id, link!.CortexTicketId);

        graph.Items = [ListItem("77", "Updated from SP", "High")];
        _ = await service.SyncSharePointSourceAsync(sourceId);

        var row = await ctx.ExternalWorkItems.SingleAsync(i => i.ExternalItemId == "77");
        Assert.Equal(ticket.Id, row.CortexTicketId);
        Assert.Equal("Updated from SP", row.Title);
    }

    [Fact]
    public async Task DiscoverFields_NonSharePoint_Throws()
    {
        await using var ctx = CreateContext();
        var service = CreateService(ctx, new FakeSharePointGraphClient());
        var conn = await service.CreateConnectionAsync(new CreateIntegrationConnectionRequest
        {
            Provider = IntegrationProvider.Jira,
            DisplayName = "J",
        });
        var source = await service.CreateSourceAsync(conn.Id, new CreateExternalWorkSourceRequest
        {
            Provider = IntegrationProvider.Jira,
            SourceType = ExternalSourceType.JiraProject,
            ExternalSourceId = "P",
            Name = "Board",
        });

        var ex = await Assert.ThrowsAsync<IntegrationApiException>(() =>
            service.DiscoverSharePointFieldsAsync(source!.Id));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task DiscoverFields_ReturnsSuggestedCortexFields()
    {
        await using var ctx = CreateContext();
        var graph = new FakeSharePointGraphClient
        {
            Columns =
            [
                FakeSharePointGraphClient.ParseJson(
                    """{"name":"Title","displayName":"Title","hidden":false,"readOnly":false}"""),
                FakeSharePointGraphClient.ParseJson(
                    """{"name":"Urgency","displayName":"Priority Level","hidden":false,"readOnly":false}"""),
                FakeSharePointGraphClient.ParseJson(
                    """{"name":"Details","displayName":"Request Details","hidden":false,"readOnly":false}"""),
            ],
        };
        var service = CreateService(ctx, graph);
        var conn = await service.CreateConnectionAsync(new CreateIntegrationConnectionRequest
        {
            Provider = IntegrationProvider.SharePoint,
            DisplayName = "C",
        });
        var source = await service.CreateSourceAsync(conn.Id, new CreateExternalWorkSourceRequest
        {
            Provider = IntegrationProvider.SharePoint,
            SourceType = ExternalSourceType.SharePointList,
            ExternalSourceId = "L1",
            Name = "L",
            ExternalUrl = ValidListUrl,
        });

        var fields = await service.DiscoverSharePointFieldsAsync(source!.Id);
        Assert.NotNull(fields);
        var list = fields!.ToList();
        Assert.Contains(list, f => f.ExternalFieldName == "Title" && f.SuggestedCortexField == CortexField.Title);
        Assert.Contains(list, f => f.ExternalFieldName == "Urgency" && f.SuggestedCortexField == CortexField.Priority);
        Assert.Contains(list, f => f.ExternalFieldName == "Details" && f.SuggestedCortexField == CortexField.Description);
    }

    [Fact]
    public async Task Sync_GraphFailure_RecordsConnectionStatus_WithoutSecretsInMessage()
    {
        await using var ctx = CreateContext();
        var (service, connId, sourceId, graph) = await SeedSharePointSourceAsync(ctx);
        graph.SiteException = new IntegrationApiException(
            403,
            "Microsoft Graph denied access. Verify app registration permissions and admin consent.");

        var ex = await Assert.ThrowsAsync<IntegrationApiException>(() => service.SyncSharePointSourceAsync(sourceId));
        Assert.Equal(403, ex.StatusCode);

        var conn = await ctx.IntegrationConnections.FirstAsync(c => c.Id == connId);
        Assert.Equal("Failed", conn.LastSyncStatus);
        Assert.NotNull(conn.LastSyncMessage);
        Assert.DoesNotContain("secret", conn.LastSyncMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer ", conn.LastSyncMessage!, StringComparison.Ordinal);
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
}
