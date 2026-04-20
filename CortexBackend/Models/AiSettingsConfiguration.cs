namespace Cortex.API.Models;

public class AiSettingsConfiguration
{
    public int Id { get; set; }

    public bool IsIntakeAssistEnabled { get; set; }
    public bool IsTriageEnabled { get; set; }
    public bool IsScreenshotInsightEnabled { get; set; }
    public bool IsSuggestedUpdatesEnabled { get; set; }
    public bool IsPriorityRecommendationEnabled { get; set; }
    public bool IsStatusRecommendationEnabled { get; set; }

    public string DefaultTextModel { get; set; } = string.Empty;
    public string DefaultVisionModel { get; set; } = string.Empty;
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

    public int? LastModifiedBy { get; set; }
    public User? LastModifiedByUser { get; set; }
    public DateTime? LastModifiedDateUtc { get; set; }
}
