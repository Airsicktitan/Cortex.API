using Cortex.API.Models;

namespace Cortex.API.DTO;

public sealed record SapReferenceSourceResponse(
    int Id,
    string Name,
    string? Description,
    SapReferenceSourceType SourceType,
    string? SystemLabel,
    string? Client,
    string? Environment,
    bool IsEnabled,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CreateSapReferenceSourceRequest(
    string Name,
    string? Description,
    SapReferenceSourceType? SourceType,
    string? SystemLabel,
    string? Client,
    string? Environment,
    bool? IsEnabled);

public sealed record UpdateSapReferenceSourceRequest(
    string Name,
    string? Description,
    SapReferenceSourceType? SourceType,
    string? SystemLabel,
    string? Client,
    string? Environment,
    bool? IsEnabled);

public sealed record SetSapReferenceSourceEnabledRequest(bool IsEnabled);

public sealed record SapTableMetadataResponse(
    int Id,
    int SapReferenceSourceId,
    string TableName,
    string? Description,
    string? Module,
    string? BusinessObject,
    string? DataDomain,
    bool IsCustom,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    int FieldCount);

public sealed record CreateSapTableMetadataRequest(
    string TableName,
    string? Description,
    string? Module,
    string? BusinessObject,
    string? DataDomain,
    bool? IsCustom,
    string? Notes);

public sealed record UpdateSapTableMetadataRequest(
    string TableName,
    string? Description,
    string? Module,
    string? BusinessObject,
    string? DataDomain,
    bool IsCustom,
    string? Notes);

public sealed record SapFieldMetadataResponse(
    int Id,
    int SapTableMetadataId,
    string FieldName,
    string? Description,
    string? DataElement,
    string? DomainName,
    string? DataType,
    int? Length,
    bool IsKey,
    bool? IsRequired,
    bool IsCustom,
    string? BusinessMeaning,
    string? ExampleValue,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CreateSapFieldMetadataRequest(
    string FieldName,
    string? Description,
    string? DataElement,
    string? DomainName,
    string? DataType,
    int? Length,
    bool? IsKey,
    bool? IsRequired,
    bool? IsCustom,
    string? BusinessMeaning,
    string? ExampleValue,
    string? Notes);

public sealed record UpdateSapFieldMetadataRequest(
    string FieldName,
    string? Description,
    string? DataElement,
    string? DomainName,
    string? DataType,
    int? Length,
    bool IsKey,
    bool? IsRequired,
    bool IsCustom,
    string? BusinessMeaning,
    string? ExampleValue,
    string? Notes);

public sealed record SapDomainValueResponse(
    int Id,
    int SapReferenceSourceId,
    string DomainName,
    string Value,
    string? Description,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CreateSapDomainValueRequest(
    string DomainName,
    string Value,
    string? Description,
    string? Notes);

public sealed record UpdateSapDomainValueRequest(
    string DomainName,
    string Value,
    string? Description,
    string? Notes);

/// <summary>Search hit: Table, Field, or DomainValue. Reference knowledge only.</summary>
public sealed record SapReferenceSearchResultDto(
    string ResultType,
    int SourceId,
    string SourceName,
    int? TableId,
    string? TableName,
    int? FieldId,
    string? FieldName,
    string Title,
    string? Subtitle,
    string? Description,
    bool? IsCustom,
    string? Module,
    string? BusinessObject,
    string RelevanceReason,
    int? DomainValueId);
