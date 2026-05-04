using System.Text.Json;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Cortex.API.Services.Integrations;
using Cortex.API.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Tests;

public class IntegrationActivityLogTests
{
    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"int-activity-{Guid.NewGuid():N}")
            .Options;
        return new CortexDbContext(options);
    }

    private static async Task<int> SeedSharePointSourceIdAsync(CortexDbContext ctx)
    {
        var service = IntegrationServiceTestFactory.Create(ctx);
        var conn = await service.CreateConnectionAsync(new CreateIntegrationConnectionRequest
        {
            Provider = IntegrationProvider.SharePoint,
            DisplayName = "C",
            IsEnabled = true,
        });
        var source = await service.CreateSourceAsync(conn.Id, new CreateExternalWorkSourceRequest
        {
            Provider = IntegrationProvider.SharePoint,
            SourceType = ExternalSourceType.SharePointList,
            ExternalSourceId = "list",
            Name = "L",
            ExternalUrl = "https://fabrikam.sharepoint.com/sites/s/Lists/T",
            IsEnabled = true,
        });
        return source!.Id;
    }

    [Fact]
    public async Task DiscoverFields_Success_WritesActivityRow_WithFieldCountMetadata()
    {
        await using var ctx = CreateContext();
        var graph = new FakeSharePointGraphClient
        {
            Columns =
            [
                FakeSharePointGraphClient.ParseJson(
                    """{"name":"Title","displayName":"Title","hidden":false,"readOnly":false}"""),
            ],
        };
        var service = IntegrationServiceTestFactory.Create(ctx, graph);
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
            ExternalUrl = "https://fabrikam.sharepoint.com/sites/support/Lists/Tickets",
        });

        var fields = await service.DiscoverSharePointFieldsAsync(source!.Id);
        Assert.NotNull(fields);

        var log = await ctx.IntegrationActivityLogs.SingleAsync();
        Assert.Equal(IntegrationActivityType.DiscoverFields, log.ActivityType);
        Assert.Equal(IntegrationActivityStatus.Success, log.Status);
        Assert.Contains("Fields discovered:", log.Message, StringComparison.Ordinal);
        Assert.Contains("fieldCount", log.MetadataJson ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task DiscoverFields_NonSharePoint_WritesFailedActivityRow()
    {
        await using var ctx = CreateContext();
        var service = IntegrationServiceTestFactory.Create(ctx);
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

        var log = await ctx.IntegrationActivityLogs.SingleAsync();
        Assert.Equal(IntegrationActivityStatus.Failed, log.Status);
        Assert.Equal(IntegrationActivityType.DiscoverFields, log.ActivityType);
        Assert.Contains("SharePoint", log.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sync_Success_WritesActivityRow_WithCounts()
    {
        await using var ctx = CreateContext();
        var graph = new FakeSharePointGraphClient
        {
            Items =
            [
                FakeSharePointGraphClient.ParseJson(
                    """{"id":"1","lastModifiedDateTime":"2024-01-15T10:00:00Z","fields":{"Title":"A"}}"""),
            ],
        };
        var service = IntegrationServiceTestFactory.Create(ctx, graph);
        var conn = await service.CreateConnectionAsync(new CreateIntegrationConnectionRequest
        {
            Provider = IntegrationProvider.SharePoint,
            DisplayName = "C",
            IsEnabled = true,
        });
        var source = await service.CreateSourceAsync(conn.Id, new CreateExternalWorkSourceRequest
        {
            Provider = IntegrationProvider.SharePoint,
            SourceType = ExternalSourceType.SharePointList,
            ExternalSourceId = "list-guid",
            Name = "Issues",
            ExternalUrl = "https://fabrikam.sharepoint.com/sites/support/Lists/Tickets",
            IsEnabled = true,
        });
        await service.ReplaceFieldMappingsAsync(source!.Id,
        [
            new ExternalFieldMappingItemRequest
            {
                ExternalFieldName = "Title",
                CortexField = CortexField.Title,
            },
        ]);

        var result = await service.SyncSharePointSourceAsync(source.Id);
        Assert.NotNull(result);

        var log = await ctx.IntegrationActivityLogs.SingleAsync();
        Assert.Equal(IntegrationActivityType.SyncSource, log.ActivityType);
        Assert.Equal(IntegrationActivityStatus.Success, log.Status);
        Assert.Equal(1, log.CreatedCount);
        Assert.Equal(0, log.ErrorCount);
        Assert.Equal(1, log.ItemCount);
    }

    [Fact]
    public async Task Sync_GraphDenied_WritesFailedActivityRow_WithSafeMessage()
    {
        await using var ctx = CreateContext();
        var graph = new FakeSharePointGraphClient();
        var service = IntegrationServiceTestFactory.Create(ctx, graph);
        var conn = await service.CreateConnectionAsync(new CreateIntegrationConnectionRequest
        {
            Provider = IntegrationProvider.SharePoint,
            DisplayName = "C",
            IsEnabled = true,
        });
        var source = await service.CreateSourceAsync(conn.Id, new CreateExternalWorkSourceRequest
        {
            Provider = IntegrationProvider.SharePoint,
            SourceType = ExternalSourceType.SharePointList,
            ExternalSourceId = "list-guid",
            Name = "Issues",
            ExternalUrl = "https://fabrikam.sharepoint.com/sites/support/Lists/Tickets",
            IsEnabled = true,
        });
        await service.ReplaceFieldMappingsAsync(source!.Id,
        [
            new ExternalFieldMappingItemRequest
            {
                ExternalFieldName = "Title",
                CortexField = CortexField.Title,
            },
        ]);

        graph.SiteException = new IntegrationApiException(403, "Microsoft Graph denied access. Check app registration.");

        var ex = await Assert.ThrowsAsync<IntegrationApiException>(() => service.SyncSharePointSourceAsync(source.Id));
        Assert.Equal(403, ex.StatusCode);

        var log = await ctx.IntegrationActivityLogs.SingleAsync();
        Assert.Equal(IntegrationActivityStatus.Failed, log.Status);
        Assert.Contains("Graph", log.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", log.ErrorMessage ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Bearer ", log.ErrorMessage ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task ManualUpsert_Success_WritesActivityRow()
    {
        await using var ctx = CreateContext();
        var service = IntegrationServiceTestFactory.Create(ctx);
        var conn = await service.CreateConnectionAsync(new CreateIntegrationConnectionRequest
        {
            Provider = IntegrationProvider.SharePoint,
            DisplayName = "C",
        });
        var source = await service.CreateSourceAsync(conn.Id, new CreateExternalWorkSourceRequest
        {
            Provider = IntegrationProvider.SharePoint,
            SourceType = ExternalSourceType.SharePointList,
            ExternalSourceId = "L",
            Name = "N",
            ExternalUrl = "https://x.sharepoint.com/sites/s/Lists/T",
        });

        _ = await service.ManualUpsertWorkItemAsync(source!.Id, new ManualUpsertExternalWorkItemRequest
        {
            ExternalItemId = "SP-2001",
            Title = "T",
        });

        var log = await ctx.IntegrationActivityLogs.SingleAsync();
        Assert.Equal(IntegrationActivityType.ManualUpsert, log.ActivityType);
        Assert.Equal(IntegrationActivityStatus.Success, log.Status);
        Assert.Contains("SP-2001", log.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetSourceActivity_ReturnsNewestFirst()
    {
        await using var ctx = CreateContext();
        var sourceId = await SeedSharePointSourceIdAsync(ctx);
        var svc = new IntegrationActivityService(ctx);
        var t0 = DateTime.UtcNow.AddMinutes(-10);
        await svc.RecordAsync(
            new IntegrationActivityLogRecordRequest
            {
                ExternalWorkSourceId = sourceId,
                ActivityType = IntegrationActivityType.SyncSource,
                Status = IntegrationActivityStatus.Success,
                StartedAtUtc = t0,
                CompletedAtUtc = t0,
                Message = "older",
            });
        var t1 = DateTime.UtcNow;
        await svc.RecordAsync(
            new IntegrationActivityLogRecordRequest
            {
                ExternalWorkSourceId = sourceId,
                ActivityType = IntegrationActivityType.SyncSource,
                Status = IntegrationActivityStatus.Success,
                StartedAtUtc = t1,
                CompletedAtUtc = t1,
                Message = "newer",
            });

        var rows = await svc.GetSourceActivityAsync(sourceId, 20, null);
        Assert.NotNull(rows);
        Assert.Equal("newer", rows![0].Message);
        Assert.Equal("older", rows[1].Message);
    }

    [Fact]
    public async Task GetSourceActivity_ClampsTake_ToMax100()
    {
        await using var ctx = CreateContext();
        var sourceId = await SeedSharePointSourceIdAsync(ctx);
        var svc = new IntegrationActivityService(ctx);
        var baseTime = DateTime.UtcNow.AddHours(-1);
        for (var i = 0; i < 101; i++)
        {
            var tick = baseTime.AddTicks(i * 10);
            await svc.RecordAsync(
                new IntegrationActivityLogRecordRequest
                {
                    ExternalWorkSourceId = sourceId,
                    ActivityType = IntegrationActivityType.ManualUpsert,
                    Status = IntegrationActivityStatus.Success,
                    StartedAtUtc = tick,
                    CompletedAtUtc = tick,
                    Message = $"m{i}",
                });
        }

        var rows = await svc.GetSourceActivityAsync(sourceId, 500, null);
        Assert.NotNull(rows);
        Assert.Equal(100, rows!.Count);
    }

    [Fact]
    public void IntegrationActivityLogResponse_Serialization_OmitsMetadataAndSecrets()
    {
        var dto = new IntegrationActivityLogResponse(
            1,
            2,
            3,
            IntegrationActivityType.SyncSource,
            IntegrationActivityStatus.Success,
            "Admin",
            DateTime.UtcNow,
            DateTime.UtcNow,
            100,
            1,
            0,
            0,
            0,
            0,
            5,
            "Created 1",
            null);
        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.DoesNotContain("metadata", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("rawJson", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
    }
}
