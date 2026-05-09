using System.Text.Json;
using Cortex.API.Services.Integrations;

namespace Cortex.API.Tests.TestDoubles;

public sealed class FakeSharePointGraphClient : ISharePointGraphClient
{
    public SharePointSiteRef Site { get; set; } = new("fake-site-id");

    public JsonElement List { get; set; } = ParseJson("{\"id\":\"fake-list-id\"}");

    public IReadOnlyList<JsonElement> Columns { get; set; } = [];

    public IReadOnlyList<JsonElement> Items { get; set; } = [];

    public Exception? SiteException { get; set; }

    public Exception? ListException { get; set; }

    public Exception? ColumnsException { get; set; }

    public Exception? ItemsException { get; set; }

    public Exception? ValidateCredentialsException { get; set; }

    public Task<SharePointSiteRef> GetSiteByPathAsync(
        string hostname,
        string siteRelativePath,
        string? tenantIdOverride,
        CancellationToken cancellationToken = default)
    {
        ThrowIf(SiteException);
        return Task.FromResult(Site);
    }

    public Task ValidateGraphApplicationCredentialsAsync(
        string? tenantIdOverride,
        CancellationToken cancellationToken = default)
    {
        ThrowIf(ValidateCredentialsException);
        return Task.CompletedTask;
    }

    public Task<JsonElement> GetListAsync(
        string siteId,
        string listId,
        string? tenantIdOverride,
        CancellationToken cancellationToken = default)
    {
        ThrowIf(ListException);
        return Task.FromResult(List.Clone());
    }

    public Task<IReadOnlyList<JsonElement>> GetListColumnsAsync(
        string siteId,
        string listId,
        string? tenantIdOverride,
        CancellationToken cancellationToken = default)
    {
        ThrowIf(ColumnsException);
        return Task.FromResult(Columns);
    }

    public Task<IReadOnlyList<JsonElement>> GetListItemsAsync(
        string siteId,
        string listId,
        string? tenantIdOverride,
        CancellationToken cancellationToken = default)
    {
        ThrowIf(ItemsException);
        return Task.FromResult(Items);
    }

    private static void ThrowIf(Exception? ex)
    {
        if (ex != null)
        {
            throw ex;
        }
    }

    public static JsonElement ParseJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.Clone();
    }
}
