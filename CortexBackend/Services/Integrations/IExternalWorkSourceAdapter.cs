using Cortex.API.Models;

namespace Cortex.API.Services.Integrations;

/// <summary>
/// Provider adapter for discovering and syncing <see cref="ExternalWorkSource"/> / <see cref="ExternalWorkItem"/> data.
/// </summary>
/// <remarks>
/// Implementations are work-board oriented: lists, projects, service tables. Reference-only integrations
/// (e.g. future SAP technical metadata or domain-value catalogs) should not be forced through this interface;
/// add parallel adapters keyed by <see cref="IntegrationConnection"/> when those features are designed.
/// </remarks>
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
    string? TypeHint,
    string? DisplayName = null,
    bool IsHidden = false,
    bool IsReadOnly = false,
    CortexField? SuggestedCortexField = null);

public sealed record ExternalWorkItemSyncBatch(int UpsertedCount, int MarkedDeletedCount);
