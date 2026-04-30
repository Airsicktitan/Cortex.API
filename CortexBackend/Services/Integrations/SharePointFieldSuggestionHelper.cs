using Cortex.API.Models;

namespace Cortex.API.Services.Integrations;

public static class SharePointFieldSuggestionHelper
{
    /// <summary>Heuristic suggestions for field mapping UX; not persisted.</summary>
    public static CortexField? SuggestCortexField(string displayName, string internalName)
    {
        var text = $"{displayName} {internalName}".ToLowerInvariant();

        if (Matches(text, "title") && !Matches(text, "subtitle"))
        {
            return CortexField.Title;
        }

        if (MatchesAny(text, "description", "details", "request details", "body"))
        {
            return CortexField.Description;
        }

        if (MatchesAny(text, "status", "state"))
        {
            return CortexField.Status;
        }

        if (MatchesAny(text, "priority", "urgency", "severity"))
        {
            return CortexField.Priority;
        }

        if (MatchesAny(text, "requester", "requested by", "created by", "submitted by"))
        {
            return CortexField.Requester;
        }

        if (MatchesAny(text, "department", "area", "function", "business unit"))
        {
            return CortexField.Department;
        }

        if (MatchesAny(text, "business owner"))
        {
            return CortexField.BusinessOwner;
        }

        if (MatchesAny(text, "assigned consultant", "syniti owner", "assigned to", "owner"))
        {
            return CortexField.SynitiOwner;
        }

        if (MatchesAny(text, "category", "module", "topic"))
        {
            return CortexField.Category;
        }

        if (MatchesAny(text, "due date", "duedate", "target date", "deadline"))
        {
            return CortexField.DueDate;
        }

        if (MatchesAny(text, "evidence", "link", " url", "attachment", "href"))
        {
            return CortexField.EvidenceUrl;
        }

        return null;
    }

    private static bool Matches(string text, string token) => text.Contains(token, StringComparison.OrdinalIgnoreCase);

    private static bool MatchesAny(string text, params string[] tokens) =>
        tokens.Any(t => text.Contains(t, StringComparison.OrdinalIgnoreCase));
}
