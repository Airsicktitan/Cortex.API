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

    public const int TicketListMinPage = 1;
    public const int TicketListMinPageSize = 1;
    public const int TicketListMaxPageSize = 100;
    public const int TicketListDefaultPage = 1;
    public const int TicketListDefaultPageSize = 25;

    public static readonly string[] AllowedTicketListSorts =
    [
        "newest-first",
        "oldest-first",
        "priority-high-low",
        "priority-low-high",
        "due-soonest",
        "most-overdue"
    ];

    public static bool TryNormalizeTicketListPage(int? page, out int normalized, out string? errorMessage)
    {
        errorMessage = null;
        if (page is null)
        {
            normalized = TicketListDefaultPage;
            return true;
        }

        if (page.Value < TicketListMinPage)
        {
            normalized = TicketListDefaultPage;
            errorMessage = $"page must be at least {TicketListMinPage}.";
            return false;
        }

        normalized = page.Value;
        return true;
    }

    public static bool TryNormalizeTicketListPageSize(int? pageSize, out int normalized, out string? errorMessage)
    {
        errorMessage = null;
        if (pageSize is null)
        {
            normalized = TicketListDefaultPageSize;
            return true;
        }

        if (pageSize.Value < TicketListMinPageSize || pageSize.Value > TicketListMaxPageSize)
        {
            normalized = TicketListDefaultPageSize;
            errorMessage =
                $"pageSize must be between {TicketListMinPageSize} and {TicketListMaxPageSize}.";
            return false;
        }

        normalized = pageSize.Value;
        return true;
    }

    public static bool TryNormalizeTicketListSort(string? sort, out string normalized, out string? errorMessage)
    {
        errorMessage = null;
        if (string.IsNullOrWhiteSpace(sort))
        {
            normalized = "newest-first";
            return true;
        }

        var trimmed = sort.Trim();
        foreach (var allowed in AllowedTicketListSorts)
        {
            if (string.Equals(trimmed, allowed, StringComparison.OrdinalIgnoreCase))
            {
                normalized = allowed;
                return true;
            }
        }

        normalized = "newest-first";
        errorMessage =
            $"sort must be one of: {string.Join(", ", AllowedTicketListSorts)}.";
        return false;
    }

    public static bool IsSlaTicketListSort(string sort) =>
        sort is "due-soonest" or "most-overdue";

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

    /// <summary>Optional ticket board filter: when provided, must be a positive identifier.</summary>
    public static bool TryValidateOptionalBoardId(int? boardId, out int? normalized, out string? errorMessage)
    {
        normalized = null;
        errorMessage = null;

        if (boardId is null)
        {
            return true;
        }

        if (boardId.Value < 1)
        {
            errorMessage = "boardId must be a positive integer when provided.";
            return false;
        }

        normalized = boardId.Value;
        return true;
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
