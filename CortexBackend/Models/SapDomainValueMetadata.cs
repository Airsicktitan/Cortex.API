namespace Cortex.API.Models;

public class SapDomainValueMetadata
{
    public int Id { get; set; }

    public int SapReferenceSourceId { get; set; }

    public string DomainName { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public SapReferenceSource SapReferenceSource { get; set; } = null!;
}
