using Cortex.API.Models;

namespace Cortex.API.Services.Integrations;

/// <summary>
/// Placeholder adapter for future Microsoft Graph list discovery. Does not call external APIs.
/// </summary>
public sealed class SharePointExternalWorkSourceAdapter : IExternalWorkSourceAdapter
{
    public IntegrationProvider Provider => IntegrationProvider.SharePoint;

    public Task<IReadOnlyList<ExternalSourceDiscoveryResult>> DiscoverSourcesAsync(
        IntegrationConnection connection,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ExternalSourceDiscoveryResult>>(
            Array.Empty<ExternalSourceDiscoveryResult>());

    public Task<IReadOnlyList<ExternalFieldDiscoveryResult>> DiscoverFieldsAsync(
        ExternalWorkSource source,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<ExternalFieldDiscoveryResult>>(
            Array.Empty<ExternalFieldDiscoveryResult>());

    public Task<ExternalWorkItemSyncBatch> SyncItemsAsync(
        ExternalWorkSource source,
        CancellationToken cancellationToken = default) =>
        Task.FromResult(new ExternalWorkItemSyncBatch(0, 0));
}
