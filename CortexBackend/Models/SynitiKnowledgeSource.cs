namespace Cortex.API.Models;

/// <summary>Catalog source for Syniti/DSP reference knowledge (advisory, not live integration).</summary>
public class SynitiKnowledgeSource
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public SynitiKnowledgeSourceType SourceType { get; set; } = SynitiKnowledgeSourceType.Manual;

    public string? Version { get; set; }

    public bool IsEnabled { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public ICollection<SynitiKnowledgeEntry> Entries { get; set; } = [];
}
