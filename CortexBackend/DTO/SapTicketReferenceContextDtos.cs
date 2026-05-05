using System.Text.Json.Serialization;

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

/// <summary>GET /api/tickets/{ticketId}/sap-reference-context — read-only matches from SAP reference catalogs (advisory).</summary>
public sealed record SapTicketReferenceContextDto(
    string TicketId,
    IReadOnlyList<SapTicketReferenceMatchDto> Matches,
    /// <summary>True when ticket text suggests SAP work but no catalog match — intake readiness only.</summary>
    bool SapIntentOnly = false);

public sealed record SapTicketReferenceMatchDto
{
    public required SapTicketReferenceMatchType MatchType { get; init; }
    public required string MatchedText { get; init; }

    public string? TableName { get; init; }
    public string? TableDescription { get; init; }
    public string? FieldName { get; init; }
    public string? FieldDescription { get; init; }

    public string? DomainName { get; init; }
    public string? DomainValue { get; init; }

    /// <summary>Short deterministic preview line when domain-fixed values exist in the catalog.</summary>
    public string? DomainValuesPreview { get; init; }

    public required string SourceName { get; init; }

    public string? Module { get; init; }
    public string? BusinessObject { get; init; }
    public string? DataDomain { get; init; }

    /// <summary>Custom flag stored on table/field metadata when present.</summary>
    public bool IsCustom { get; init; }

    /// <summary>YY-/ZZ-prefix or catalog custom — extension-style field.</summary>
    public bool LikelyCustomerExtensionField { get; init; }

    public required SapTicketReferenceMatchConfidence Confidence { get; init; }

    /// <summary>Deterministic catalogue match rationale (technical).</summary>
    public required string Reason { get; init; }

    public required string MatchStrengthLabel { get; init; }

    /// <summary>Reviewer-facing provenance narrative (deterministic).</summary>
    public required string SourceReason { get; init; }

    [JsonIgnore] public int? TableId { get; init; }

    [JsonIgnore] public int? FieldId { get; init; }

    [JsonIgnore] public int? SourceId { get; init; }
}

/// <summary>Catalog row for deterministic ticket text matching (tests and detection).</summary>
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
    string? DomainName,
    bool FieldIsCustom);
