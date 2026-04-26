namespace Cortex.API.Models;

public static class CortexMemoryEventType
{
    public const string RelatedTicketShown = "RelatedTicketShown";
    public const string RelatedTicketClicked = "RelatedTicketClicked";
    public const string AiSuggestionAccepted = "AiSuggestionAccepted";
    public const string OwnerOverridden = "OwnerOverridden";
    public const string PriorityOverridden = "PriorityOverridden";
    public const string StatusOverridden = "StatusOverridden";

    private static readonly HashSet<string> KnownTypes = new(StringComparer.Ordinal)
    {
        RelatedTicketShown,
        RelatedTicketClicked,
        AiSuggestionAccepted,
        OwnerOverridden,
        PriorityOverridden,
        StatusOverridden,
    };

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value) && KnownTypes.Contains(value.Trim());
}
