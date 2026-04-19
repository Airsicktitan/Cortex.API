namespace Cortex.API.DTO;

/// <summary>Optional client snapshot when saving a ticket after using Improve Request in the same session.</summary>
public sealed class IntakeAssistSaveMetrics
{
    public bool IntakeAssistUsedBeforeSave { get; set; }

    public string? LastIntakeClarityState { get; set; }

    public int? LastIntakeMissingDetailCount { get; set; }
}
