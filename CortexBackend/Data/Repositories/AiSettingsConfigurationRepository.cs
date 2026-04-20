using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Data.Repositories;

public sealed class AiSettingsConfigurationRepository(CortexDbContext context)
    : IAiSettingsConfigurationRepository
{
    private readonly CortexDbContext _context = context;

    public async Task<AiSettingsConfiguration?> GetAsync()
    {
        return await _context.AiSettingsConfigurations
            .Include(configuration => configuration.LastModifiedByUser)
            .OrderBy(configuration => configuration.Id)
            .FirstOrDefaultAsync();
    }

    public async Task UpsertAsync(AiSettingsConfiguration configuration)
    {
        var existingConfiguration = await _context.AiSettingsConfigurations
            .OrderBy(existing => existing.Id)
            .FirstOrDefaultAsync();

        if (existingConfiguration is null)
        {
            configuration.Id = 0;
            await _context.AiSettingsConfigurations.AddAsync(configuration);
            return;
        }

        existingConfiguration.IsIntakeAssistEnabled = configuration.IsIntakeAssistEnabled;
        existingConfiguration.IsTriageEnabled = configuration.IsTriageEnabled;
        existingConfiguration.IsScreenshotInsightEnabled = configuration.IsScreenshotInsightEnabled;
        existingConfiguration.IsSuggestedUpdatesEnabled = configuration.IsSuggestedUpdatesEnabled;
        existingConfiguration.IsPriorityRecommendationEnabled = configuration.IsPriorityRecommendationEnabled;
        existingConfiguration.IsStatusRecommendationEnabled = configuration.IsStatusRecommendationEnabled;
        existingConfiguration.DefaultTextModel = configuration.DefaultTextModel;
        existingConfiguration.DefaultVisionModel = configuration.DefaultVisionModel;
        existingConfiguration.Temperature = configuration.Temperature;
        existingConfiguration.MaxTokens = configuration.MaxTokens;
        existingConfiguration.TimeoutSeconds = configuration.TimeoutSeconds;
        existingConfiguration.RetryCount = configuration.RetryCount;
        existingConfiguration.AdvisoryOnlyMode = configuration.AdvisoryOnlyMode;
        existingConfiguration.AllowStatusRecommendation = configuration.AllowStatusRecommendation;
        existingConfiguration.AllowPriorityRecommendation = configuration.AllowPriorityRecommendation;
        existingConfiguration.SuggestionOnlyMode = configuration.SuggestionOnlyMode;
        existingConfiguration.ConfidenceThreshold = configuration.ConfidenceThreshold;
        existingConfiguration.MaxScreenshotAttachmentCount = configuration.MaxScreenshotAttachmentCount;
        existingConfiguration.LastModifiedBy = configuration.LastModifiedBy;
        existingConfiguration.LastModifiedDateUtc = configuration.LastModifiedDateUtc;
    }

    public async Task AddAuditEntryAsync(AiSettingsAuditEntry auditEntry)
    {
        await _context.AiSettingsAuditEntries.AddAsync(auditEntry);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
