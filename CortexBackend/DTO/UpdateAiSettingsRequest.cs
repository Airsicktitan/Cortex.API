namespace Cortex.API.DTO;

public sealed class UpdateAiSettingsRequest
{
    public bool IsIntakeAssistEnabled { get; set; }
    public bool IsTriageEnabled { get; set; }
    public bool IsScreenshotInsightEnabled { get; set; }
    public bool IsSuggestedUpdatesEnabled { get; set; }
    public bool IsPriorityRecommendationEnabled { get; set; }
    public bool IsStatusRecommendationEnabled { get; set; }

    public string? DefaultTextModel { get; set; }
    public string? DefaultVisionModel { get; set; }
    public double Temperature { get; set; }
    public int MaxTokens { get; set; }
    public int TimeoutSeconds { get; set; }
    public int RetryCount { get; set; }
    public bool AdvisoryOnlyMode { get; set; }

    public bool AllowStatusRecommendation { get; set; }
    public bool AllowPriorityRecommendation { get; set; }
    public bool SuggestionOnlyMode { get; set; }
    public double ConfidenceThreshold { get; set; }
    public int MaxScreenshotAttachmentCount { get; set; }
}
