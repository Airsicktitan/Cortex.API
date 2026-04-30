using System.Text.Json;
using Cortex.API.Models;

namespace Cortex.API.Services.Integrations;

public sealed class SharePointExternalWorkSourceAdapter(ISharePointGraphClient graphClient) : IExternalWorkSourceAdapter
{
    private readonly ISharePointGraphClient _graphClient = graphClient;

    public IntegrationProvider Provider => IntegrationProvider.SharePoint;

    public Task<IReadOnlyList<ExternalSourceDiscoveryResult>> DiscoverSourcesAsync(
        IntegrationConnection connection,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ExternalSourceDiscoveryResult>>(Array.Empty<ExternalSourceDiscoveryResult>());

    public async Task<IReadOnlyList<ExternalFieldDiscoveryResult>> DiscoverFieldsAsync(
        ExternalWorkSource source,
        CancellationToken cancellationToken = default)
    {
        if (source.Provider != IntegrationProvider.SharePoint || source.SourceType != ExternalSourceType.SharePointList)
        {
            throw new IntegrationApiException(400, "Field discovery is only supported for SharePoint list sources.");
        }

        if (string.IsNullOrWhiteSpace(source.ExternalSourceId))
        {
            throw new IntegrationApiException(400, "ExternalSourceId (SharePoint list id) is required.");
        }

        if (!SharePointSiteUrlParser.TryParseListPageUrl(source.ExternalUrl, out var hostname, out var sitePath, out var err))
        {
            throw new IntegrationApiException(400, err ?? "Invalid SharePoint ExternalUrl.");
        }

        var tenant = source.IntegrationConnection?.TenantId;
        var site = await _graphClient.GetSiteByPathAsync(hostname, sitePath, tenant, cancellationToken).ConfigureAwait(false);

        // Validate list exists
        _ = await _graphClient.GetListAsync(site.Id, source.ExternalSourceId, tenant, cancellationToken).ConfigureAwait(false);

        var columns = await _graphClient.GetListColumnsAsync(site.Id, source.ExternalSourceId, tenant, cancellationToken)
            .ConfigureAwait(false);

        var list = new List<ExternalFieldDiscoveryResult>(columns.Count);
        foreach (var col in columns)
        {
            if (!col.TryGetProperty("name", out var nameEl) || nameEl.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var internalName = nameEl.GetString();
            if (string.IsNullOrWhiteSpace(internalName))
            {
                continue;
            }

            var displayName = col.TryGetProperty("displayName", out var dnEl) && dnEl.ValueKind == JsonValueKind.String
                ? dnEl.GetString()
                : internalName;
            var hidden = col.TryGetProperty("hidden", out var hEl) && hEl.ValueKind == JsonValueKind.True;
            var readOnly = col.TryGetProperty("readOnly", out var roEl) && roEl.ValueKind == JsonValueKind.True;

            var typeHint = GuessColumnType(col);
            var suggested = SharePointFieldSuggestionHelper.SuggestCortexField(displayName ?? internalName, internalName);

            list.Add(new ExternalFieldDiscoveryResult(
                internalName,
                internalName,
                typeHint,
                displayName,
                hidden,
                readOnly,
                suggested));
        }

        return list;
    }

    public Task<ExternalWorkItemSyncBatch> SyncItemsAsync(
        ExternalWorkSource source,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ExternalWorkItemSyncBatch(0, 0));

    private static string? GuessColumnType(JsonElement col)
    {
        if (col.TryGetProperty("text", out _))
        {
            return "text";
        }

        if (col.TryGetProperty("choice", out _))
        {
            return "choice";
        }

        if (col.TryGetProperty("dateTime", out _))
        {
            return "dateTime";
        }

        if (col.TryGetProperty("personOrGroup", out _))
        {
            return "personOrGroup";
        }

        if (col.TryGetProperty("lookup", out _))
        {
            return "lookup";
        }

        if (col.TryGetProperty("url", out _))
        {
            return "url";
        }

        if (col.TryGetProperty("number", out _))
        {
            return "number";
        }

        return null;
    }
}
