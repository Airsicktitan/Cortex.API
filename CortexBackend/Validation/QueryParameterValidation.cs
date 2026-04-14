namespace Cortex.API.Validation;

/// <summary>
/// Shared rules for query and route parameters used for filtering, exports, and paging.
/// </summary>
public static class QueryParameterValidation
{
    public const int MaxFilterStringLength = 200;
    public const int MinNotificationTake = 0;
    public const int MaxNotificationTake = 100;

    public static readonly string[] AllowedExportFormats = ["csv"];

    /// <summary>
    /// Validates a filter segment (route or query) for length and characters that must never appear in ticket filters.
    /// </summary>
    public static bool TryValidateSafeFilterString(string? value, out string trimmed, out string? errorMessage)
    {
        trimmed = string.Empty;
        errorMessage = null;

        if (value is null || string.IsNullOrWhiteSpace(value))
        {
            errorMessage = "Value cannot be empty.";
            return false;
        }

        trimmed = value.Trim();
        if (trimmed.Length > MaxFilterStringLength)
        {
            errorMessage = $"Value must be at most {MaxFilterStringLength} characters.";
            return false;
        }

        if (trimmed.IndexOfAny([';', '\r', '\n', '\0']) >= 0
            || trimmed.Contains("--", StringComparison.Ordinal)
            || trimmed.Contains("/*", StringComparison.Ordinal))
        {
            errorMessage = "Value contains unsupported characters.";
            return false;
        }

        return true;
    }

    /// <summary>
    /// Notification feed: default 20 when omitted; preserves existing behavior where 0 is coerced to 1 in the service layer.
    /// </summary>
    public static bool TryNormalizeNotificationTake(int? take, out int normalized, out string? errorMessage)
    {
        errorMessage = null;
        if (take is null)
        {
            normalized = 20;
            return true;
        }

        if (take.Value < MinNotificationTake || take.Value > MaxNotificationTake)
        {
            normalized = 0;
            errorMessage = $"take must be between {MinNotificationTake} and {MaxNotificationTake}.";
            return false;
        }

        normalized = take.Value;
        return true;
    }

    public static bool IsAllowedPriority(string priority, IReadOnlyList<string> allowedPriorities, out string canonical)
    {
        foreach (var allowed in allowedPriorities)
        {
            if (string.Equals(priority, allowed, StringComparison.OrdinalIgnoreCase))
            {
                canonical = allowed;
                return true;
            }
        }

        canonical = priority;
        return false;
    }

    public static bool IsAllowedExportFormat(string? format, out string? errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(format))
        {
            errorMessage = "Only CSV export is currently supported.";
            return false;
        }

        var trimmed = format.Trim();
        foreach (var allowed in AllowedExportFormats)
        {
            if (string.Equals(trimmed, allowed, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        errorMessage = "Only CSV export is currently supported.";
        return false;
    }
}
