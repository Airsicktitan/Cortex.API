namespace Cortex.API.Configuration;

/// <summary>Standard OpenAI API settings for Phase 1 advisory ticket triage.</summary>
public class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    public string? ApiKey { get; set; }

    /// <summary>Chat model id (e.g. gpt-4o-mini).</summary>
    public string? Model { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(ApiKey) &&
        !string.IsNullOrWhiteSpace(Model);
}
