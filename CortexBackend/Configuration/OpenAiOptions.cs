namespace Cortex.API.Configuration;

/// <summary>Standard OpenAI API settings for Phase 1 advisory ticket triage.</summary>
public class OpenAiOptions
{
    public const string SectionName = "OpenAI";
    public const string DefaultEmbeddingModel = "text-embedding-3-small";

    public string? ApiKey { get; set; }

    /// <summary>Chat model id (e.g. gpt-4o-mini).</summary>
    public string? Model { get; set; }

    /// <summary>Embedding model id for Cortex Memory v2 semantic retrieval.</summary>
    public string? EmbeddingModel { get; set; }

    /// <summary>Optional batch advisory language for deterministic rebalance suggestions.</summary>
    public bool EnableRebalanceAdvisory { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(Model);

    public string ResolvedEmbeddingModel =>
        string.IsNullOrWhiteSpace(EmbeddingModel)
            ? DefaultEmbeddingModel
            : EmbeddingModel.Trim();
}
