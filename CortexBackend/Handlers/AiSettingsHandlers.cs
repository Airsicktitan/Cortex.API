using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;

namespace Cortex.API.Handlers;

public static class AiSettingsHandlers
{
    public static async Task<IResult> GetAiSettings(IAiSettingsService aiSettingsService)
    {
        var configuration = await aiSettingsService.GetAsync();
        return Results.Ok(configuration.ToResponse());
    }

    public static async Task<IResult> UpdateAiSettings(
        UpdateAiSettingsRequest request,
        IAiSettingsService aiSettingsService)
    {
        try
        {
            var configuration = new AiSettingsConfiguration
            {
                IsIntakeAssistEnabled = request.IsIntakeAssistEnabled,
                IsTriageEnabled = request.IsTriageEnabled,
                IsScreenshotInsightEnabled = request.IsScreenshotInsightEnabled,
                IsSuggestedUpdatesEnabled = request.IsSuggestedUpdatesEnabled,
                IsPriorityRecommendationEnabled = request.IsPriorityRecommendationEnabled,
                IsStatusRecommendationEnabled = request.IsStatusRecommendationEnabled,
                DefaultTextModel = request.DefaultTextModel ?? string.Empty,
                DefaultVisionModel = request.DefaultVisionModel ?? string.Empty,
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens,
                TimeoutSeconds = request.TimeoutSeconds,
                RetryCount = request.RetryCount,
                AdvisoryOnlyMode = request.AdvisoryOnlyMode,
                AllowStatusRecommendation = request.AllowStatusRecommendation,
                AllowPriorityRecommendation = request.AllowPriorityRecommendation,
                SuggestionOnlyMode = request.SuggestionOnlyMode,
                ConfidenceThreshold = request.ConfidenceThreshold,
                MaxScreenshotAttachmentCount = request.MaxScreenshotAttachmentCount,
            };

            var savedConfiguration = await aiSettingsService.SaveAsync(configuration);
            return Results.Ok(savedConfiguration.ToResponse());
        }
        catch (ArgumentException)
        {
            return SafeErrorResponses.BadRequest();
        }
    }
}
