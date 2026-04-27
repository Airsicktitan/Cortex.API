namespace Cortex.API.DTO;

/// <summary>
/// Advisory learning signal derived from prior ticket outcomes.
/// Never used to mutate routing — surfaces grounded patterns for human reviewers.
/// </summary>
public sealed class CortexLearningSignalDto
{
    /// <summary>One of: Owner, Semantic, Rule, Workload, Risk.</summary>
    public string SignalType { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>One of: High, Medium, Low.</summary>
    public string Confidence { get; set; } = string.Empty;

    public List<string> SupportingFacts { get; set; } = [];
}
