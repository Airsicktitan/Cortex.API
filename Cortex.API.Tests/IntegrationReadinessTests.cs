using System.Text.Json;
using Cortex.API.Configuration;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Cortex.API.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Cortex.API.Tests;

public class IntegrationReadinessTests
{
    private const string ValidListUrl =
        "https://fabrikam.sharepoint.com/sites/support/Lists/Tickets";

    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"readiness-{Guid.NewGuid():N}")
            .Options;
        return new CortexDbContext(options);
    }

    private static SharePointGraphOptions ConfiguredGraphOptions() =>
        new()
        {
            TenantId = "11111111-1111-1111-1111-111111111111",
            ClientId = "configured-client-id",
            ClientSecret = "configured-client-secret-not-exposed-in-readiness",
        };

    private static async Task<(ExternalIntegrationService Service, int SourceId)> SeedSharePointListAsync(
        CortexDbContext ctx,
        bool connectionEnabled = true,
        bool sourceEnabled = true,
        string? externalUrl = null,
        string? externalSourceId = null,
        IntegrationProvider provider = IntegrationProvider.SharePoint,
        ExternalSourceType sourceType = ExternalSourceType.SharePointList,
        SharePointGraphOptions? graphOptions = null,
        int mappingCount = 0)
    {
        var service = IntegrationServiceTestFactory.Create(ctx, graphOptions: graphOptions ?? new SharePointGraphOptions());
        var conn = await service.CreateConnectionAsync(new CreateIntegrationConnectionRequest
        {
            Provider = IntegrationProvider.SharePoint,
            DisplayName = "Conn",
            IsEnabled = connectionEnabled,
            TenantId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
        });
        var resolvedUrl = externalUrl is null
            ? ValidListUrl
            : string.IsNullOrWhiteSpace(externalUrl)
                ? null
                : externalUrl;
        var source = await service.CreateSourceAsync(conn.Id, new CreateExternalWorkSourceRequest
        {
            Provider = provider,
            SourceType = sourceType,
            ExternalSourceId = externalSourceId ?? "list-guid",
            Name = "List",
            ExternalUrl = resolvedUrl,
            IsEnabled = sourceEnabled,
        });

        if (mappingCount > 0)
        {
            var items = Enumerable.Range(0, mappingCount)
                .Select(i => new ExternalFieldMappingItemRequest
                {
                    ExternalFieldName = $"MapField{i}",
                    CortexField = CortexField.Title,
                    IsRequired = false,
                })
                .ToList();
            await service.ReplaceFieldMappingsAsync(source!.Id, items);
        }

        return (service, source!.Id);
    }

    [Fact]
    public async Task Readiness_MissingSource_ReturnsNull()
    {
        await using var ctx = CreateContext();
        var svc = IntegrationServiceTestFactory.Create(ctx, graphOptions: ConfiguredGraphOptions());
        var r = await svc.GetSourceReadinessAsync(99999);
        Assert.Null(r);
    }

    [Fact]
    public async Task Readiness_DisabledConnection_BlocksDiscoverAndSync()
    {
        await using var ctx = CreateContext();
        var (svc, sid) = await SeedSharePointListAsync(ctx, connectionEnabled: false, graphOptions: ConfiguredGraphOptions());
        var r = await svc.GetSourceReadinessAsync(sid);
        Assert.NotNull(r);
        Assert.False(r!.CanDiscoverFields);
        Assert.False(r.CanSync);
        Assert.Contains(r.Checks, c => c.Key == "connectionEnabled" && c.Status == IntegrationReadinessCheckStatus.Failed);
    }

    [Fact]
    public async Task Readiness_DisabledSource_BlocksDiscoverAndSync()
    {
        await using var ctx = CreateContext();
        var (svc, sid) = await SeedSharePointListAsync(ctx, sourceEnabled: false, graphOptions: ConfiguredGraphOptions());
        var r = await svc.GetSourceReadinessAsync(sid);
        Assert.NotNull(r);
        Assert.False(r!.CanDiscoverFields);
        Assert.False(r.CanSync);
        Assert.Contains(r.Checks, c => c.Key == "sourceEnabled" && c.Status == IntegrationReadinessCheckStatus.Failed);
    }

    [Fact]
    public async Task Readiness_NonSharePoint_NotReadyForLive()
    {
        await using var ctx = CreateContext();
        var (svc, sid) = await SeedSharePointListAsync(ctx, provider: IntegrationProvider.Jira, sourceType: ExternalSourceType.JiraProject, graphOptions: ConfiguredGraphOptions());
        var r = await svc.GetSourceReadinessAsync(sid);
        Assert.NotNull(r);
        Assert.False(r!.CanDiscoverFields);
        Assert.False(r.CanSync);
        Assert.Contains(r.Checks, c => c.Key == "providerSharePoint" && c.Status == IntegrationReadinessCheckStatus.Warning);
    }

    [Fact]
    public async Task Readiness_MissingUrl_Fails()
    {
        await using var ctx = CreateContext();
        var (svc, sid) = await SeedSharePointListAsync(ctx, externalUrl: "", graphOptions: ConfiguredGraphOptions());
        var r = await svc.GetSourceReadinessAsync(sid);
        Assert.NotNull(r);
        Assert.False(r!.CanDiscoverFields);
        Assert.Contains(r.Checks, c => c.Key == "externalUrl" && c.Status == IntegrationReadinessCheckStatus.Failed);
    }

    [Fact]
    public async Task Readiness_MissingListId_Fails()
    {
        await using var ctx = CreateContext();
        var service = IntegrationServiceTestFactory.Create(ctx, graphOptions: ConfiguredGraphOptions());
        var conn = await service.CreateConnectionAsync(new CreateIntegrationConnectionRequest
        {
            Provider = IntegrationProvider.SharePoint,
            DisplayName = "C",
            TenantId = "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
        });
        var source = await service.CreateSourceAsync(conn.Id, new CreateExternalWorkSourceRequest
        {
            Provider = IntegrationProvider.SharePoint,
            SourceType = ExternalSourceType.SharePointList,
            ExternalSourceId = "temp",
            Name = "L",
            ExternalUrl = ValidListUrl,
        });
        Assert.NotNull(source);
        var sourceId = source!.Id;
        var entity = await ctx.ExternalWorkSources.FirstAsync(s => s.Id == sourceId);
        entity.ExternalSourceId = "   ";
        await ctx.SaveChangesAsync();

        var r = await service.GetSourceReadinessAsync(sourceId);
        Assert.NotNull(r);
        Assert.False(r!.CanDiscoverFields);
        Assert.Contains(r.Checks, c => c.Key == "externalSourceId" && c.Status == IntegrationReadinessCheckStatus.Failed);
    }

    [Fact]
    public async Task Readiness_MissingGraphConfig_Fails()
    {
        await using var ctx = CreateContext();
        var (svc, sid) = await SeedSharePointListAsync(ctx, graphOptions: new SharePointGraphOptions());
        var r = await svc.GetSourceReadinessAsync(sid);
        Assert.NotNull(r);
        Assert.False(r!.CanDiscoverFields);
        Assert.Contains(r.Checks, c => c.Key == "sharePointGraphApp" && c.Status == IntegrationReadinessCheckStatus.Failed);
    }

    [Fact]
    public async Task Readiness_ValidSharePointList_CanDiscoverFields_WithoutMappings()
    {
        await using var ctx = CreateContext();
        var (svc, sid) = await SeedSharePointListAsync(ctx, graphOptions: ConfiguredGraphOptions(), mappingCount: 0);
        var r = await svc.GetSourceReadinessAsync(sid);
        Assert.NotNull(r);
        Assert.True(r!.CanDiscoverFields);
        Assert.False(r.CanSync);
        Assert.Contains(r.Checks, c => c.Key == "fieldMappings" && c.Status == IntegrationReadinessCheckStatus.Warning);
    }

    [Fact]
    public async Task Readiness_WithMappings_CanSync()
    {
        await using var ctx = CreateContext();
        var (svc, sid) = await SeedSharePointListAsync(ctx, graphOptions: ConfiguredGraphOptions(), mappingCount: 1);
        var r = await svc.GetSourceReadinessAsync(sid);
        Assert.NotNull(r);
        Assert.True(r!.CanDiscoverFields);
        Assert.True(r.CanSync);
        Assert.True(r.IsReady);
        Assert.Contains(r.Checks, c => c.Key == "fieldMappings" && c.Status == IntegrationReadinessCheckStatus.Passed);
    }

    [Fact]
    public async Task Readiness_Response_DoesNotExposeClientSecret()
    {
        await using var ctx = CreateContext();
        var ultraSecret = "ultra-secret-value-do-not-leak-9999";
        var graphOpts = new SharePointGraphOptions
        {
            TenantId = "tenant-for-readiness-test",
            ClientId = "app-id-readiness-test",
            ClientSecret = ultraSecret,
        };
        var (svc, sid) = await SeedSharePointListAsync(ctx, graphOptions: graphOpts, mappingCount: 1);
        var r = await svc.GetSourceReadinessAsync(sid);
        Assert.NotNull(r);
        var json = JsonSerializer.Serialize(r);
        Assert.DoesNotContain(ultraSecret, json, StringComparison.Ordinal);
        Assert.DoesNotContain("app-id-readiness-test", json, StringComparison.Ordinal);
        Assert.DoesNotContain("tenant-for-readiness-test", json, StringComparison.OrdinalIgnoreCase);
    }
}
