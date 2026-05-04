namespace Cortex.API.Models;

/// <summary>
/// A catalog of SAP table/field reference knowledge for ticket intelligence.
/// Separate from <see cref="ExternalWorkSource"/> (work ingestion).
/// </summary>
public class SapReferenceSource
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public SapReferenceSourceType SourceType { get; set; } = SapReferenceSourceType.Manual;

    public string? SystemLabel { get; set; }

    public string? Client { get; set; }

    public string? Environment { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<SapTableMetadata> Tables { get; set; } = [];

    public ICollection<SapDomainValueMetadata> DomainValues { get; set; } = [];
}
