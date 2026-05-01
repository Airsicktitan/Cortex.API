namespace Cortex.API.DTO;

public enum SapTicketReferenceMatchType
{
    Table = 0,
    Field = 1,
    DomainValue = 2,
}

public enum SapTicketReferenceMatchConfidence
{
    High = 0,
    Medium = 1,
    Low = 2,
}

/// <summary>GET /api/tickets/{ticketId}/sap-reference-context — read-only matches from stored SAP reference catalogs.</summary>
public sealed record SapTicketReferenceContextDto(
    string TicketId,
    IReadOnlyList<SapTicketReferenceMatchDto> Matches);

public sealed record SapTicketReferenceMatchDto(
    SapTicketReferenceMatchType MatchType,
    string MatchedText,
    string? TableName,
    string? TableDescription,
    string? FieldName,
    string? FieldDescription,
    string? DomainName,
    string? DomainValue,
    string SourceName,
    string? Module,
    string? BusinessObject,
    string? DataDomain,
    bool IsCustom,
    SapTicketReferenceMatchConfidence Confidence,
    string Reason,
    int? TableId,
    int? FieldId,
    int? SourceId);

/// <summary>Catalog row for deterministic ticket text matching (tests and in-memory detection).</summary>
public readonly record struct SapTicketCatalogTable(
    int Id,
    int SourceId,
    string SourceName,
    string TableName,
    string? Description,
    string? Module,
    string? BusinessObject,
    string? DataDomain,
    bool IsCustom);

public readonly record struct SapTicketCatalogField(
    int Id,
    int TableMetadataId,
    int SourceId,
    string SourceName,
    string TableName,
    string? TableDescription,
    string? Module,
    string? BusinessObject,
    string? DataDomain,
    bool TableIsCustom,
    string FieldName,
    string? FieldDescription,
    bool FieldIsCustom);
