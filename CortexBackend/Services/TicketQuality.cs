namespace Cortex.API.Services;

public enum TicketQuality
{
    Ugly,
    Bad,
    Good,
}

/// <summary>
/// Lightweight heuristic classifier. No ML — just word count and specific-term density.
/// Used for post-processing only: Good tickets get title preservation and tighter missing-detail caps.
/// </summary>
public static class TicketQualityClassifier
{
    private static readonly string[] SpecificityTerms =
    [
        "error", "fail", "failure", "exception",
        "module", "transaction", "workflow", "process",
        "validation", "upload", "template", "batch",
        "field", "column", "record", "row", "file",
        "blank", "empty", "null", "invalid", "missing",
        "message", "code", "log", "id",
    ];

    public static TicketQuality Classify(string? title, string? description)
    {
        var titleWords = WordCount(title);
        var descWords = WordCount(description);
        var totalWords = titleWords + descWords;

        if (totalWords < 10)
        {
            return TicketQuality.Ugly;
        }

        var combined = $"{title ?? ""} {description ?? ""}".ToLowerInvariant();
        var specificTermCount = SpecificityTerms.Count(
            t => combined.Contains(t, StringComparison.Ordinal));

        if (descWords >= 15 && specificTermCount >= 3)
        {
            return TicketQuality.Good;
        }

        return TicketQuality.Bad;
    }

    private static int WordCount(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return text.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
