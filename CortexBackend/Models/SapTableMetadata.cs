namespace Cortex.API.Models;

public class SapTableMetadata
{
    public int Id { get; set; }

    public int SapReferenceSourceId { get; set; }

    /// <summary>Normalized SAP table name (typically uppercase).</summary>
    public string TableName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Module { get; set; }

    public string? BusinessObject { get; set; }

    public string? DataDomain { get; set; }

    public bool IsCustom { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public SapReferenceSource SapReferenceSource { get; set; } = null!;

    public ICollection<SapFieldMetadata> Fields { get; set; } = [];
}
