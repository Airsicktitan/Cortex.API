namespace Cortex.API.Services;

/// <summary>Known intake/routing category labels used when constraining model output.</summary>
public static class CortexAiCategoryVocabulary
{
    public static readonly string[] Values =
    [
        "Root-cause fix",
        "Automation",
        "Documentation",
        "Training",
        "Monitoring",
        "Process change",
    ];

    public static string? TryMatch(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var candidate = raw.Trim();
        foreach (var allowed in Values)
        {
            if (string.Equals(candidate, allowed, StringComparison.OrdinalIgnoreCase))
            {
                return allowed;
            }
        }

        return null;
    }
}
