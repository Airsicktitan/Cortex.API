using System.Text.Json;
using System.Text.Json.Serialization;
using Cortex.API.Configuration;
using Cortex.API.Data.Repositories;
using Cortex.API.Models;
using Microsoft.Extensions.Options;

namespace Cortex.API.Services;

public sealed class AiSettingsService(
    IAiSettingsConfigurationRepository repository,
    IUserContextService userContextService,
    IOptions<OpenAiOptions> openAiOptions)
    : IAiSettingsService
{
    private const string FallbackModelName = "gpt-4o-mini";
    private const int DefaultMaxTokens = 1800;
    private const int DefaultTimeoutSeconds = 120;
    private const int DefaultRetryCount = 0;
    private const int DefaultMaxScreenshotAttachmentCount = 5;
    private const double DefaultTemperature = 0.2;
    private const double DefaultConfidenceThreshold = 0.7;

    private static readonly JsonSerializerOptions SnapshotJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
    };

    private readonly IAiSettingsConfigurationRepository _repository = repository;
    private readonly IUserContextService _userContextService = userContextService;
    private readonly OpenAiOptions _openAiOptions = openAiOptions.Value;

    private AiSettingsConfiguration? _cachedConfiguration;
    private bool _isLoaded;

    public async Task<AiSettingsConfiguration> GetAsync()
    {
        if (_isLoaded && _cachedConfiguration is not null)
        {
            return Clone(_cachedConfiguration);
        }

        var configuration = await _repository.GetAsync();
        if (configuration is null)
        {
            configuration = CreateDefaultConfiguration();
            await _repository.UpsertAsync(configuration);
            await _repository.SaveChangesAsync();
            configuration = await _repository.GetAsync() ?? configuration;
        }

        _cachedConfiguration = Clone(configuration);
        _isLoaded = true;
        return Clone(_cachedConfiguration);
    }

    public async Task<AiSettingsConfiguration> SaveAsync(AiSettingsConfiguration configuration)
    {
        Validate(configuration);

        var before = await GetAsync();
        var currentUser = await _userContextService.GetCurrentUserAsync();
        var normalizedConfiguration = Clone(configuration);
        normalizedConfiguration.LastModifiedBy = currentUser.Id;
        normalizedConfiguration.LastModifiedDateUtc = DateTime.UtcNow;

        await _repository.UpsertAsync(normalizedConfiguration);
        await _repository.AddAuditEntryAsync(new AiSettingsAuditEntry
        {
            ChangedBy = currentUser.Id,
            ChangedDateUtc = normalizedConfiguration.LastModifiedDateUtc.Value,
            BeforeSnapshotJson = SerializeSnapshot(before),
            AfterSnapshotJson = SerializeSnapshot(normalizedConfiguration),
        });
        await _repository.SaveChangesAsync();

        var savedConfiguration = await _repository.GetAsync() ?? normalizedConfiguration;
        _cachedConfiguration = Clone(savedConfiguration);
        _isLoaded = true;
        return Clone(_cachedConfiguration);
    }

    private AiSettingsConfiguration CreateDefaultConfiguration()
    {
        var defaultModel = string.IsNullOrWhiteSpace(_openAiOptions.Model)
            ? FallbackModelName
            : _openAiOptions.Model.Trim();

        return new AiSettingsConfiguration
        {
            IsIntakeAssistEnabled = true,
            IsTriageEnabled = true,
            IsScreenshotInsightEnabled = true,
            IsSuggestedUpdatesEnabled = false,
            IsPriorityRecommendationEnabled = true,
            IsStatusRecommendationEnabled = true,
            DefaultTextModel = defaultModel,
            DefaultVisionModel = defaultModel,
            Temperature = DefaultTemperature,
            MaxTokens = DefaultMaxTokens,
            TimeoutSeconds = DefaultTimeoutSeconds,
            RetryCount = DefaultRetryCount,
            AdvisoryOnlyMode = false,
            AllowStatusRecommendation = true,
            AllowPriorityRecommendation = true,
            SuggestionOnlyMode = false,
            ConfidenceThreshold = DefaultConfidenceThreshold,
            MaxScreenshotAttachmentCount = DefaultMaxScreenshotAttachmentCount,
        };
    }

    private static AiSettingsConfiguration Clone(AiSettingsConfiguration configuration)
    {
        return new AiSettingsConfiguration
        {
            Id = configuration.Id,
            IsIntakeAssistEnabled = configuration.IsIntakeAssistEnabled,
            IsTriageEnabled = configuration.IsTriageEnabled,
            IsScreenshotInsightEnabled = configuration.IsScreenshotInsightEnabled,
            IsSuggestedUpdatesEnabled = configuration.IsSuggestedUpdatesEnabled,
            IsPriorityRecommendationEnabled = configuration.IsPriorityRecommendationEnabled,
            IsStatusRecommendationEnabled = configuration.IsStatusRecommendationEnabled,
            DefaultTextModel = configuration.DefaultTextModel,
            DefaultVisionModel = configuration.DefaultVisionModel,
            Temperature = configuration.Temperature,
            MaxTokens = configuration.MaxTokens,
            TimeoutSeconds = configuration.TimeoutSeconds,
            RetryCount = configuration.RetryCount,
            AdvisoryOnlyMode = configuration.AdvisoryOnlyMode,
            AllowStatusRecommendation = configuration.AllowStatusRecommendation,
            AllowPriorityRecommendation = configuration.AllowPriorityRecommendation,
            SuggestionOnlyMode = configuration.SuggestionOnlyMode,
            ConfidenceThreshold = configuration.ConfidenceThreshold,
            MaxScreenshotAttachmentCount = configuration.MaxScreenshotAttachmentCount,
            LastModifiedBy = configuration.LastModifiedBy,
            LastModifiedByUser = configuration.LastModifiedByUser,
            LastModifiedDateUtc = configuration.LastModifiedDateUtc,
        };
    }

    private static void Validate(AiSettingsConfiguration configuration)
    {
        if (string.IsNullOrWhiteSpace(configuration.DefaultTextModel))
        {
            throw new ArgumentException("Default text model is required.", nameof(configuration));
        }

        if (string.IsNullOrWhiteSpace(configuration.DefaultVisionModel))
        {
            throw new ArgumentException("Default vision model is required.", nameof(configuration));
        }

        configuration.DefaultTextModel = configuration.DefaultTextModel.Trim();
        configuration.DefaultVisionModel = configuration.DefaultVisionModel.Trim();

        if (configuration.DefaultTextModel.Length > 200)
        {
            throw new ArgumentException(
                "Default text model must be 200 characters or fewer.",
                nameof(configuration));
        }

        if (configuration.DefaultVisionModel.Length > 200)
        {
            throw new ArgumentException(
                "Default vision model must be 200 characters or fewer.",
                nameof(configuration));
        }

        if (configuration.Temperature < 0 || configuration.Temperature > 2)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                "Temperature must be between 0 and 2.");
        }

        if (configuration.MaxTokens < 1 || configuration.MaxTokens > 4000)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                "Max tokens must be between 1 and 4000.");
        }

        if (configuration.TimeoutSeconds < 5 || configuration.TimeoutSeconds > 300)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                "Timeout seconds must be between 5 and 300.");
        }

        if (configuration.RetryCount < 0 || configuration.RetryCount > 3)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                "Retry count must be between 0 and 3.");
        }

        if (configuration.ConfidenceThreshold < 0 || configuration.ConfidenceThreshold > 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                "Confidence threshold must be between 0 and 1.");
        }

        if (configuration.MaxScreenshotAttachmentCount < 1 ||
            configuration.MaxScreenshotAttachmentCount > 8)
        {
            throw new ArgumentOutOfRangeException(
                nameof(configuration),
                "Max screenshot attachment count must be between 1 and 8.");
        }
    }

    private static string SerializeSnapshot(AiSettingsConfiguration configuration)
    {
        return JsonSerializer.Serialize(
            new AiSettingsAuditSnapshot
            {
                IsIntakeAssistEnabled = configuration.IsIntakeAssistEnabled,
                IsTriageEnabled = configuration.IsTriageEnabled,
                IsScreenshotInsightEnabled = configuration.IsScreenshotInsightEnabled,
                IsSuggestedUpdatesEnabled = configuration.IsSuggestedUpdatesEnabled,
                IsPriorityRecommendationEnabled = configuration.IsPriorityRecommendationEnabled,
                IsStatusRecommendationEnabled = configuration.IsStatusRecommendationEnabled,
                DefaultTextModel = configuration.DefaultTextModel,
                DefaultVisionModel = configuration.DefaultVisionModel,
                Temperature = configuration.Temperature,
                MaxTokens = configuration.MaxTokens,
                TimeoutSeconds = configuration.TimeoutSeconds,
                RetryCount = configuration.RetryCount,
                AdvisoryOnlyMode = configuration.AdvisoryOnlyMode,
                AllowStatusRecommendation = configuration.AllowStatusRecommendation,
                AllowPriorityRecommendation = configuration.AllowPriorityRecommendation,
                SuggestionOnlyMode = configuration.SuggestionOnlyMode,
                ConfidenceThreshold = configuration.ConfidenceThreshold,
                MaxScreenshotAttachmentCount = configuration.MaxScreenshotAttachmentCount,
            },
            SnapshotJsonOptions);
    }

    private sealed class AiSettingsAuditSnapshot
    {
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
    }
}
