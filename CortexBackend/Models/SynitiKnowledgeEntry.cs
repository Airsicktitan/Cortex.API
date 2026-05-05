namespace Cortex.API.Models;

public class SynitiKnowledgeEntry
{
    public int Id { get; set; }

    public int SynitiKnowledgeSourceId { get; set; }

    /// <summary>Primary term or phrase (e.g. &quot;DSP&quot;, &quot;Value Mapping&quot;).</summary>
    public string Term { get; set; } = string.Empty;

    public SynitiKnowledgeCategory Category { get; set; } = SynitiKnowledgeCategory.Other;

    public string ShortDefinition { get; set; } = string.Empty;

    public string? BusinessMeaning { get; set; }

    public string? TechnicalMeaning { get; set; }

    /// <summary>Optional free-text hints (reviewer-facing).</summary>
    public string? CommonSignals { get; set; }

    /// <summary>Semicolon-separated related terms for preview.</summary>
    public string? RelatedTerms { get; set; }

    /// <summary>Semicolon or newline separated example phrases for deterministic matching.</summary>
    public string? ExamplePhrases { get; set; }

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public SynitiKnowledgeSource SynitiKnowledgeSource { get; set; } = null!;
}
