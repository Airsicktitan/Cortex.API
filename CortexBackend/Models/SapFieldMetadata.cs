namespace Cortex.API.Models;

public class SapFieldMetadata
{
    public int Id { get; set; }

    public int SapTableMetadataId { get; set; }

    /// <summary>Normalized SAP field name (typically uppercase).</summary>
    public string FieldName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? DataElement { get; set; }

    public string? DomainName { get; set; }

    public string? DataType { get; set; }

    public int? Length { get; set; }

    public bool IsKey { get; set; }

    public bool? IsRequired { get; set; }

    public bool IsCustom { get; set; }

    public string? BusinessMeaning { get; set; }

    public string? ExampleValue { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public SapTableMetadata SapTableMetadata { get; set; } = null!;
}
