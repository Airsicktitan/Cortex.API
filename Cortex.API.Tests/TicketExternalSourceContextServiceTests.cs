using System.Text.Json;
using System.Text.Json.Serialization;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Cortex.API.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Tests;

public class TicketExternalSourceContextServiceTests
{
    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"ext-src-ctx-{Guid.NewGuid():N}")
            .Options;
        return new CortexDbContext(options);
    }

    private static async Task<ExternalWorkSourceResponse?> CreateSourceAsync(IExternalIntegrationService service)
    {
        var connection = await service.CreateConnectionAsync(
            new CreateIntegrationConnectionRequest
            {
                Provider = IntegrationProvider.SharePoint,
                DisplayName = "Conn",
            });

        return await service.CreateSourceAsync(
            connection.Id,
            new CreateExternalWorkSourceRequest
            {
                Provider = IntegrationProvider.SharePoint,
                SourceType = ExternalSourceType.SharePointList,
                ExternalSourceId = $"list-{Guid.NewGuid():N}",
                Name = "SAP Support Requests",
            });
    }

    [Fact]
    public async Task GetExternalSourceContexts_NoLinkedItem_ReturnsEmptyArray()
    {
        await using var ctx = CreateContext();
        var service = IntegrationServiceTestFactory.Create(ctx);

        var list = await service.GetExternalSourceContextsForTicketAsync("TICKET-99");

        Assert.Empty(list);
    }

    [Fact]
    public async Task GetExternalSourceContexts_Linked_ReturnsSourceFields_OrderedByLastSeenDesc()
    {
        await using var ctx = CreateContext();
        var integration = IntegrationServiceTestFactory.Create(ctx);
        var source = await CreateSourceAsync(integration);

        var earlier = DateTime.UtcNow.AddHours(-2);
        var later = DateTime.UtcNow.AddHours(-1);

        ctx.ExternalWorkItems.Add(
            new ExternalWorkItem
            {
                ExternalWorkSourceId = source!.Id,
                Provider = IntegrationProvider.SharePoint,
                ExternalItemId = "older",
                Title = "Old item",
                CortexTicketId = "TICKET-2",
                Status = "Open",
                Priority = "High",
                Requester = "req@x",
                AssignedTo = "owner@x",
                Department = "IT",
                Category = "SAP",
                LastSeenUtc = earlier,
                LastModifiedUtc = earlier,
                CreatedAtUtc = earlier,
                RawJson = """{"secret":"x"}""",
            });
        ctx.ExternalWorkItems.Add(
            new ExternalWorkItem
            {
                ExternalWorkSourceId = source.Id,
                Provider = IntegrationProvider.SharePoint,
                ExternalItemId = "SP-2001",
                Title = "External title",
                ExternalUrl = "https://tenant.sharepoint.com/x",
                CortexTicketId = "TICKET-2",
                LastSeenUtc = later,
                LastModifiedUtc = later,
                CreatedAtUtc = later,
                RawJson = "{}",
            });
        await ctx.SaveChangesAsync();

        var queryService = IntegrationServiceTestFactory.Create(ctx);
        var list = (await queryService.GetExternalSourceContextsForTicketAsync("TICKET-2")).ToList();

        Assert.Equal(2, list.Count);
        Assert.Equal("SP-2001", list[0].ExternalItemId);
        Assert.Equal("older", list[1].ExternalItemId);

        var primary = list[0];
        Assert.Equal("TICKET-2", primary.TicketId);
        Assert.True(primary.ExternalWorkItemId > 0);
        Assert.Equal("External title", primary.ExternalTitle);
        Assert.Equal("SAP Support Requests", primary.SourceName);
        Assert.Equal(IntegrationProvider.SharePoint, primary.Provider);
        Assert.Equal(ExternalSourceType.SharePointList, primary.SourceType);
        Assert.Equal("https://tenant.sharepoint.com/x", primary.ExternalUrl);

        var json = JsonSerializer.Serialize(
            list,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() },
            });
        Assert.DoesNotContain("rawJson", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.Ordinal);

    }

    [Fact]
    public async Task GetExternalSourceContexts_ExcludesDeletedItems()
    {
        await using var ctx = CreateContext();
        var integration = IntegrationServiceTestFactory.Create(ctx);
        var source = await CreateSourceAsync(integration);
        var now = DateTime.UtcNow;

        ctx.ExternalWorkItems.Add(
            new ExternalWorkItem
            {
                ExternalWorkSourceId = source!.Id,
                Provider = IntegrationProvider.SharePoint,
                ExternalItemId = "gone",
                Title = "Deleted",
                CortexTicketId = "T-1",
                IsDeleted = true,
                LastSeenUtc = now,
                CreatedAtUtc = now,
                RawJson = "{}",
            });
        await ctx.SaveChangesAsync();

        var queryService = IntegrationServiceTestFactory.Create(ctx);
        var list = await queryService.GetExternalSourceContextsForTicketAsync("T-1");

        Assert.Empty(list);
    }
}
