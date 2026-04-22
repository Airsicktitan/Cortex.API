namespace Cortex.API.Services;

/// <summary>
/// Post-model normalization: never trust raw AI strings; map to system vocabulary and adjust confidence.
/// </summary>
public static class CortexAiAssessmentConstraintMapper
{
    private static readonly string[] RiskOrder = ["Low", "Medium", "High"];

    public static string NormalizeRisk(string? raw, ref decimal confidence)
    {
        var normalized = NormalizeRiskCore(raw);
        if (normalized is null && !string.IsNullOrWhiteSpace(raw))
        {
            confidence = Math.Max(0m, confidence - 0.08m);
            return "Low";
        }

        return normalized ?? "Low";
    }

    public static string? NormalizeRiskCore(string? raw)
    {
        var candidate = raw?.Trim();
        if (string.IsNullOrEmpty(candidate))
        {
            return null;
        }

        foreach (var allowed in RiskOrder)
        {
            if (string.Equals(candidate, allowed, StringComparison.OrdinalIgnoreCase))
            {
                return allowed;
            }
        }

        return null;
    }

    /// <summary>Exact configured priority name match (case-insensitive).</summary>
    public static string? TryMatchConfiguredPriorityName(
        string? raw,
        IReadOnlyList<TicketTriagePriorityOption> priorities)
    {
        if (string.IsNullOrWhiteSpace(raw) || priorities.Count == 0)
        {
            return null;
        }

        var trimmed = raw.Trim();
        foreach (var priority in priorities)
        {
            if (string.Equals(priority.Name, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return priority.Name;
            }
        }

        return null;
    }

    /// <summary>
    /// When the model value is not in the configured SLA list, infer a configured priority using light synonym heuristics.
    /// </summary>
    public static string? ResolvePrioritySynonym(
        string? rawModelValue,
        IReadOnlyList<TicketTriagePriorityOption> priorities)
    {
        if (priorities.Count == 0)
        {
            return null;
        }

        // Lower target hours = tighter SLA tier in typical configs — treat index 0 as the "highest" urgency tier.
        var ordered = priorities
            .OrderBy(p => p.TargetHours)
            .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var raw = rawModelValue?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            return null;
        }

        var lower = raw.ToLowerInvariant();

        if (lower.Contains("critical", StringComparison.Ordinal) || lower.Contains("p0", StringComparison.Ordinal))
        {
            return ordered[0].Name;
        }

        if (lower.Contains("urgent", StringComparison.Ordinal)
            || lower.Contains("asap", StringComparison.Ordinal)
            || lower.Contains("high", StringComparison.Ordinal)
            || lower.Contains("important", StringComparison.Ordinal))
        {
            return ordered[0].Name;
        }

        if (lower.Contains("low", StringComparison.Ordinal) || lower.Contains("minor", StringComparison.Ordinal))
        {
            return ordered[^1].Name;
        }

        if (lower.Contains("medium", StringComparison.Ordinal) || lower.Contains("normal", StringComparison.Ordinal))
        {
            var mid = ordered.Count / 2;
            return ordered[mid].Name;
        }

        return null;
    }

    /// <summary>
    /// When the model value is not in the configured SLA list, infer a configured priority using light synonym heuristics.
    /// </summary>
    public static string? ResolvePriorityFallback(
        string? rawModelValue,
        IReadOnlyList<TicketTriagePriorityOption> priorities,
        ref decimal confidence)
    {
        var mapped = ResolvePrioritySynonym(rawModelValue, priorities);
        if (mapped is not null)
        {
            confidence = Math.Max(0m, confidence - 0.1m);
        }

        return mapped;
    }

    public static string ResolvePriorityOrTicketDefault(
        string? suggestedFromTriage,
        string ticketCurrentPriority,
        IReadOnlyList<TicketTriagePriorityOption> priorities,
        ref decimal confidence)
    {
        if (priorities.Count == 0)
        {
            return string.IsNullOrWhiteSpace(suggestedFromTriage)
                ? ticketCurrentPriority.Trim()
                : suggestedFromTriage.Trim();
        }

        var names = priorities.Select(p => p.Name).ToList();
        if (!string.IsNullOrWhiteSpace(suggestedFromTriage))
        {
            var hit = names.FirstOrDefault(n =>
                string.Equals(n, suggestedFromTriage.Trim(), StringComparison.OrdinalIgnoreCase));
            if (hit is not null)
            {
                return hit;
            }
        }

        var ticketHit = names.FirstOrDefault(n =>
            string.Equals(n, ticketCurrentPriority.Trim(), StringComparison.OrdinalIgnoreCase));
        if (ticketHit is not null)
        {
            confidence = Math.Max(0m, confidence - 0.06m);
            return ticketHit;
        }

        confidence = Math.Max(0m, confidence - 0.12m);
        return names[0];
    }
}
