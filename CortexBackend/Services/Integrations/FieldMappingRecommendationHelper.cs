using Cortex.API.Models;

namespace Cortex.API.Services.Integrations;

/// <summary>Advisory labels and copy for discovered / planning field mapping (never applied automatically).</summary>
public static class FieldMappingRecommendationHelper
{
    /// <summary>
    /// SharePoint list standard or system columns — rough heuristic for "custom" chip in UX.
    /// </summary>
    public static bool IsLikelyCustomSharePointColumn(string internalName)
    {
        if (string.IsNullOrWhiteSpace(internalName))
        {
            return false;
        }

        var n = internalName.Trim();
        if (n.StartsWith('_') || n.StartsWith("odata.", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var builtins = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Title",
            "Author",
            "Editor",
            "Created",
            "Modified",
            "Attachments",
            "ContentType",
            "GUID",
            "ID",
            "FileLeafRef",
            "FileRef",
            "FileDirRef",
            "LinkTitle",
            "LinkTitleNoMenu",
            "DocIcon",
            "ItemChildCount",
            "FolderChildCount",
            "SortBehavior",
            "CopySource",
            "Edit",
            "UIVersionString",
            "ParentVersionString",
            "CheckoutUser",
            "CheckedOutUserId",
        };

        return !builtins.Contains(n);
    }

    public static (string? RecommendationReason, string ConfidenceLabel) DescribeSharePointSuggestion(
        CortexField? suggested,
        string displayName,
        string internalName)
    {
        var label = $"{displayName} ({internalName})".Trim();
        if (suggested is { } cf)
        {
            var cortex = cf.ToString();
            var reason = $"Name resembles common “{cortex}” patterns in SharePoint lists.";
            var confidence = IsHighConfidenceSharePointMatch(internalName, displayName, cf) ? "Strong" : "Suggested";
            return (reason, confidence);
        }

        return (
            $"No automatic pattern match for “{label}”. Choose a Cortex field manually after review.",
            "Possible");
    }

    private static bool IsHighConfidenceSharePointMatch(string internalName, string displayName, CortexField suggested)
    {
        var text = $"{displayName} {internalName}".ToLowerInvariant();
        return suggested switch
        {
            CortexField.Title => text.Contains("title", StringComparison.Ordinal) &&
                                 !text.Contains("subtitle", StringComparison.Ordinal),
            CortexField.Description => text.Contains("description", StringComparison.Ordinal) ||
                                       text.Contains("details", StringComparison.Ordinal),
            CortexField.Status => text.Contains("status", StringComparison.Ordinal) ||
                                  text.Contains("state", StringComparison.Ordinal),
            CortexField.Priority => text.Contains("priority", StringComparison.Ordinal) ||
                                    text.Contains("severity", StringComparison.Ordinal),
            _ => false,
        };
    }
}
