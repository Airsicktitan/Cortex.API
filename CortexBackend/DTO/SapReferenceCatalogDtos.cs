namespace Cortex.API.DTO;

/// <summary>Read-only SAP reference listing for Configuration visibility (advisory; stored metadata only).</summary>
public sealed record SapReferenceCatalogListResponse(
    IReadOnlyList<SapReferenceCatalogEntryDto> Entries);

/// <summary>No internal database identifiers — trust and transparency UI only.</summary>
public sealed record SapReferenceCatalogEntryDto(
    string RowKind,
    string TableName,
    string? FieldName,
    string? TableDescription,
    string? FieldDescription,
    string? BusinessObject,
    string? Module,
    string? Domain,
    bool? IsKey,
    bool? IsRequired,
    bool? IsCustomField,
    bool LikelyCustomSapField,
    string SourceName,
    string SourceType,
    bool SourceIsEnabled,
    int FieldCount,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
