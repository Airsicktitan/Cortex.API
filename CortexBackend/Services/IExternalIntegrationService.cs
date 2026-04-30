using Cortex.API.DTO;
using Cortex.API.Models;

namespace Cortex.API.Services;

/// <summary>
/// Admin and API operations for integration <em>connections</em> and external <em>work</em> sources/items.
/// </summary>
/// <remarks>
/// Methods here focus on <c>ExternalWork*</c> flows (SharePoint/Jira/ServiceNow-style ingestion).
/// Other provider capabilities—for example SAP as an enterprise reference or context source—can later
/// use separate services while sharing <see cref="IntegrationConnection"/> for identity and configuration.
/// </remarks>
public interface IExternalIntegrationService
{
    Task<IReadOnlyList<IntegrationConnectionResponse>> ListConnectionsAsync(CancellationToken cancellationToken = default);
    Task<IntegrationConnectionResponse?> GetConnectionAsync(int id, CancellationToken cancellationToken = default);
    Task<IntegrationConnectionResponse> CreateConnectionAsync(CreateIntegrationConnectionRequest request, CancellationToken cancellationToken = default);
    Task<IntegrationConnectionResponse?> UpdateConnectionAsync(int id, UpdateIntegrationConnectionRequest request, CancellationToken cancellationToken = default);
    Task<IntegrationConnectionResponse?> SetConnectionEnabledAsync(int id, bool isEnabled, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExternalWorkSourceResponse>?> ListSourcesAsync(int connectionId, CancellationToken cancellationToken = default);
    Task<ExternalWorkSourceResponse?> CreateSourceAsync(int connectionId, CreateExternalWorkSourceRequest request, CancellationToken cancellationToken = default);
    Task<ExternalWorkSourceResponse?> UpdateSourceAsync(int sourceId, UpdateExternalWorkSourceRequest request, CancellationToken cancellationToken = default);
    Task<ExternalWorkSourceResponse?> SetSourceEnabledAsync(int sourceId, bool isEnabled, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExternalFieldMappingResponse>?> GetFieldMappingsAsync(int sourceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExternalFieldMappingResponse>?> ReplaceFieldMappingsAsync(
        int sourceId,
        IReadOnlyList<ExternalFieldMappingItemRequest> mappings,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExternalBoardMappingResponse>?> GetBoardMappingsAsync(int sourceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExternalBoardMappingResponse>?> ReplaceBoardMappingsAsync(
        int sourceId,
        IReadOnlyList<ExternalBoardMappingItemRequest> mappings,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ExternalWorkItemResponse>?> ListWorkItemsAsync(int sourceId, CancellationToken cancellationToken = default);
    Task<ExternalWorkItemResponse?> GetWorkItemAsync(int itemId, CancellationToken cancellationToken = default);
    Task<ExternalWorkItemResponse?> ManualUpsertWorkItemAsync(int sourceId, ManualUpsertExternalWorkItemRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SharePointDiscoveredFieldResponse>?> DiscoverSharePointFieldsAsync(int sourceId, CancellationToken cancellationToken = default);

    Task<ExternalSourceSyncResponse?> SyncSharePointSourceAsync(int sourceId, CancellationToken cancellationToken = default);
}
