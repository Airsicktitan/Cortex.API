using Cortex.API.Models;

namespace Cortex.API.Services.Integrations;

public interface IExternalWorkSourceAdapter
{
    IntegrationProvider Provider { get; }

    Task<IReadOnlyList<ExternalSourceDiscoveryResult>> DiscoverSourcesAsync(
        IntegrationConnection connection,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExternalFieldDiscoveryResult>> DiscoverFieldsAsync(
        ExternalWorkSource source,
        CancellationToken cancellationToken = default);

    Task<ExternalWorkItemSyncBatch> SyncItemsAsync(
        ExternalWorkSource source,
        CancellationToken cancellationToken = default);
}

public sealed record ExternalSourceDiscoveryResult(
    string ExternalSourceId,
    string DisplayName,
    string? ExternalUrl,
    ExternalSourceType SuggestedSourceType);

public sealed record ExternalFieldDiscoveryResult(
    string FieldName,
    string? FieldKey,
    string? TypeHint);

public sealed record ExternalWorkItemSyncBatch(int UpsertedCount, int MarkedDeletedCount);
