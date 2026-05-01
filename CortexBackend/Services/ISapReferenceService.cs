using Cortex.API.DTO;

namespace Cortex.API.Services;

public interface ISapReferenceService
{
    Task<IReadOnlyList<SapReferenceSourceResponse>> ListSourcesAsync(CancellationToken cancellationToken = default);
    Task<SapReferenceSourceResponse?> GetSourceAsync(int sourceId, CancellationToken cancellationToken = default);
    Task<SapReferenceSourceResponse> CreateSourceAsync(CreateSapReferenceSourceRequest request, CancellationToken cancellationToken = default);
    Task<SapReferenceSourceResponse?> UpdateSourceAsync(int sourceId, UpdateSapReferenceSourceRequest request, CancellationToken cancellationToken = default);
    Task<SapReferenceSourceResponse?> SetSourceEnabledAsync(int sourceId, bool isEnabled, CancellationToken cancellationToken = default);
    Task<bool> DeleteSourceAsync(int sourceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SapTableMetadataResponse>> ListTablesAsync(int sourceId, CancellationToken cancellationToken = default);
    Task<SapTableMetadataResponse?> GetTableAsync(int tableId, CancellationToken cancellationToken = default);
    Task<SapTableMetadataResponse?> CreateTableAsync(int sourceId, CreateSapTableMetadataRequest request, CancellationToken cancellationToken = default);
    Task<SapTableMetadataResponse?> UpdateTableAsync(int tableId, UpdateSapTableMetadataRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteTableAsync(int tableId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SapFieldMetadataResponse>> ListFieldsAsync(int tableId, CancellationToken cancellationToken = default);
    Task<SapFieldMetadataResponse?> CreateFieldAsync(int tableId, CreateSapFieldMetadataRequest request, CancellationToken cancellationToken = default);
    Task<SapFieldMetadataResponse?> UpdateFieldAsync(int fieldId, UpdateSapFieldMetadataRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteFieldAsync(int fieldId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SapDomainValueResponse>> ListDomainValuesAsync(int sourceId, CancellationToken cancellationToken = default);
    Task<SapDomainValueResponse?> CreateDomainValueAsync(int sourceId, CreateSapDomainValueRequest request, CancellationToken cancellationToken = default);
    Task<SapDomainValueResponse?> UpdateDomainValueAsync(int domainValueId, UpdateSapDomainValueRequest request, CancellationToken cancellationToken = default);
    Task<bool> DeleteDomainValueAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SapReferenceSearchResultDto>> SearchAsync(string query, CancellationToken cancellationToken = default);
}
