using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cortex.API.Configuration;
using Cortex.API.DTO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cortex.API.Services;

/// <summary>
/// OpenAI client for the user-facing Improve Request flow. Mirrors the HTTP/JSON conventions of
/// <see cref="TicketTriageAiService"/> (json_object response_format, fail-open unavailable payloads,
/// structured logging) but is strictly assistive: it never mutates tickets and never touches the
/// reviewer triage vocabulary. All validation is applied locally before returning.
/// </summary>
public sealed class TicketIntakeAssistAiService : ITicketIntakeAssistAiService
{
    private const string OpenAiChatCompletionsUrl = "https://api.openai.com/v1/chat/completions";
    private const int MaxMissingDetails = 4;
    private const int MaxMissingDetailLength = 240;
    private const int MaxSummaryLength = 240;
    private const int MaxImprovedDescriptionLength = 4000;
    private const int MaxGuidanceLength = 240;
    private const int FeatureMaxTokens = 1400;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly IAiSettingsService _aiSettingsService;
    private readonly ITicketIntakeAssistPromptBuilder _promptBuilder;
    private readonly ILogger<TicketIntakeAssistAiService> _logger;

    public TicketIntakeAssistAiService(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        IAiSettingsService aiSettingsService,
        ITicketIntakeAssistPromptBuilder promptBuilder,
        ILogger<TicketIntakeAssistAiService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _aiSettingsService = aiSettingsService;
        _promptBuilder = promptBuilder;
        _logger = logger;
    }

    public async Task<IntakeAssistResponse> ImproveAsync(
        IntakeAssistInput input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return Unavailable("Improve Request is not configured. Set OpenAI:ApiKey.");
        }

        var aiSettings = await _aiSettingsService.GetAsync();
        if (!aiSettings.IsIntakeAssistEnabled)
        {
            return Unavailable("Improve Request is disabled by an administrator.");
        }

        if (string.IsNullOrWhiteSpace(aiSettings.DefaultTextModel))
        {
            return Unavailable("Improve Request is not configured. Set a default text model.");
        }

        var systemPrompt = _promptBuilder.BuildSystemPrompt();
        var userPrompt = BuildUserPrompt(input);

        var requestBody = new OpenAiChatRequest
        {
            Model = aiSettings.DefaultTextModel.Trim(),
            Messages =
            [
                new ChatMessage { Role = "system", Content = systemPrompt },
                new ChatMessage { Role = "user", Content = userPrompt },
            ],
            Temperature = aiSettings.Temperature,
            MaxTokens = AiRequestExecution.ResolveMaxTokens(aiSettings.MaxTokens, FeatureMaxTokens),
            ResponseFormat = new ResponseFormatPayload { Type = "json_object" },
        };

        for (var attempt = 0; attempt <= aiSettings.RetryCount; attempt++)
        {
            using var timeoutScope = AiRequestExecution.CreateTimeoutScope(
                cancellationToken,
                aiSettings.TimeoutSeconds);

            int? httpStatusCode = null;
            string? reasonPhrase = null;
            string? responseBody = null;

            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, OpenAiChatCompletionsUrl);
                httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                httpRequest.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _options.ApiKey.Trim());

                var json = JsonSerializer.Serialize(requestBody, JsonSerializerOptions);
                httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(httpRequest, timeoutScope.Token);
                httpStatusCode = (int)response.StatusCode;
                reasonPhrase = response.ReasonPhrase;
                responseBody = await response.Content.ReadAsStringAsync(timeoutScope.Token);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "OpenAI intake-assist error response. Attempt={Attempt} StatusCode={StatusCode} ReasonPhrase={ReasonPhrase} Body={ResponseBody}",
                        attempt + 1,
                        httpStatusCode,
                        reasonPhrase,
                        responseBody);

                    if (attempt < aiSettings.RetryCount
                        && AiRequestExecution.ShouldRetry(response.StatusCode))
                    {
                        await Task.Delay(
                            AiRequestExecution.GetRetryDelay(attempt + 1),
                            cancellationToken);
                        continue;
                    }

                    return Unavailable("Improve Request is unavailable right now. Try again in a moment.");
                }

                var outer = JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(
                    responseBody,
                    JsonSerializerOptions);
                var content = outer?.Choices?.FirstOrDefault()?.Message?.Content;
                if (string.IsNullOrWhiteSpace(content))
                {
                    return Unavailable("Improve Request returned no content.");
                }

                IntakeAssistAiResponse? parsed;
                try
                {
                    parsed = JsonSerializer.Deserialize<IntakeAssistAiResponse>(
                        content,
                        JsonSerializerOptions);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Improve Request returned non-JSON content. Content={Content}",
                        content);
                    return Unavailable("Improve Request returned an unexpected response.");
                }

                if (parsed is null)
                {
                    return Unavailable("Improve Request returned no content.");
                }

                var quality = TicketQualityClassifier.Classify(input.Title, input.Description);
                return Sanitize(parsed, input, quality);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "OpenAI intake-assist timed out. Attempt={Attempt} TimeoutSeconds={TimeoutSeconds}",
                    attempt + 1,
                    aiSettings.TimeoutSeconds);

                if (attempt < aiSettings.RetryCount)
                {
                    await Task.Delay(
                        AiRequestExecution.GetRetryDelay(attempt + 1),
                        cancellationToken);
                    continue;
                }

                return Unavailable("Improve Request timed out. Try again in a moment.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var httpRequestException = ex as HttpRequestException;
                var httpRequestStatus = httpRequestException?.StatusCode;

                _logger.LogWarning(
                    ex,
                    "OpenAI intake-assist request failed. Attempt={Attempt} ExceptionMessage={ExceptionMessage} HttpStatusCode={HttpStatusCode} ReasonPhrase={ReasonPhrase} ResponseBody={ResponseBody} HttpRequestExceptionStatusCode={HttpRequestExceptionStatusCode}",
                    attempt + 1,
                    ex.Message,
                    httpStatusCode,
                    reasonPhrase,
                    responseBody ?? "(not available)",
                    httpRequestStatus);

                if (attempt < aiSettings.RetryCount
                    && httpRequestException is not null
                    && AiRequestExecution.ShouldRetry(httpRequestStatus))
                {
                    await Task.Delay(
                        AiRequestExecution.GetRetryDelay(attempt + 1),
                        cancellationToken);
                    continue;
                }

                return Unavailable("Improve Request is unavailable right now. Try again in a moment.");
            }
        }

        return Unavailable("Improve Request is unavailable right now. Try again in a moment.");
    }

    private static string BuildUserPrompt(IntakeAssistInput input)
    {
        var sb = new StringBuilder(1024);

        sb.AppendLine("Requester draft:");
        sb.AppendLine(AiPromptUserText.WrapLines(
            [
                $"Title: {input.Title}",
                "Description:",
                input.Description
            ]));

        if (!string.IsNullOrWhiteSpace(input.BoardName))
        {
            sb.Append("Board context (for background only, do not reference in output): ");
            sb.AppendLine(input.BoardName.Trim());
        }

        sb.AppendLine();
        sb.AppendLine("Produce the JSON object now, following every rule.");

        return sb.ToString();
    }

    /// <summary>
    /// Local validation + shaping. Trims, clamps lengths, drops empties, normalizes clarityState,
    /// and enforces the empty-missingDetails invariant for ready_for_execution. Output of this method
    /// is always safe to return to the client unchanged.
    /// </summary>
    private static IntakeAssistResponse Sanitize(
        IntakeAssistAiResponse raw,
        IntakeAssistInput input,
        TicketQuality quality)
    {
        var suggestedSummary = Truncate(raw.SuggestedSummary?.Trim(), MaxSummaryLength);
        var improvedDescription = Truncate(raw.ImprovedDescription?.Trim(), MaxImprovedDescriptionLength);

        // Good tickets get a tighter cap on missing details — only minor optional items.
        var maxMissing = quality == TicketQuality.Good ? 2 : MaxMissingDetails;
        var missingDetails = (raw.MissingDetails ?? [])
            .Select(value => value?.Trim())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => Truncate(value, MaxMissingDetailLength)!)
            .Take(maxMissing)
            .ToList();

        var clarityState = NormalizeClarityState(raw.ClarityState, missingDetails.Count);

        if (clarityState == IntakeAssistClarityStates.ReadyForExecution)
        {
            missingDetails.Clear();
        }

        var guidanceMessage =
            Truncate(raw.GuidanceMessage?.Trim(), MaxGuidanceLength)
            ?? DefaultGuidance(clarityState);

        // Good input: always preserve the original title — the AI must not downgrade specificity.
        if (quality == TicketQuality.Good && !string.IsNullOrWhiteSpace(input.Title))
        {
            suggestedSummary = Truncate(input.Title.Trim(), MaxSummaryLength);
        }
        else if (string.IsNullOrWhiteSpace(suggestedSummary))
        {
            suggestedSummary = Truncate(input.Title?.Trim(), MaxSummaryLength);
        }

        if (string.IsNullOrWhiteSpace(improvedDescription))
        {
            improvedDescription = Truncate(input.Description?.Trim(), MaxImprovedDescriptionLength);
        }

        return new IntakeAssistResponse
        {
            SuggestedSummary = suggestedSummary,
            ImprovedDescription = improvedDescription,
            MissingDetails = missingDetails,
            ClarityState = clarityState,
            GuidanceMessage = guidanceMessage,
            Unavailable = false,
        };
    }

    private static string NormalizeClarityState(string? raw, int missingCount)
    {
        var candidate = raw?.Trim().ToLowerInvariant();

        if (candidate == IntakeAssistClarityStates.ReadyForExecution
            || candidate == IntakeAssistClarityStates.RequiresClarification
            || candidate == IntakeAssistClarityStates.WouldHaveRequiredFollowUp)
        {
            return candidate!;
        }

        return missingCount == 0
            ? IntakeAssistClarityStates.ReadyForExecution
            : IntakeAssistClarityStates.RequiresClarification;
    }

    private static string DefaultGuidance(string clarityState) => clarityState switch
    {
        IntakeAssistClarityStates.ReadyForExecution =>
            "A reviewer can work from this draft as written.",
        IntakeAssistClarityStates.WouldHaveRequiredFollowUp =>
            "You can submit; filling in the items below will reduce reviewer back-and-forth.",
        _ =>
            "Add the items below so a reviewer can route and act without chasing you for basics.",
    };

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= max ? value : value[..max].TrimEnd();
    }

    private static IntakeAssistResponse Unavailable(string reason) =>
        new()
        {
            Unavailable = true,
            UnavailableReason = reason,
            MissingDetails = [],
        };

    private sealed class OpenAiChatRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<ChatMessage> Messages { get; set; } = [];
        public double Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("response_format")]
        public ResponseFormatPayload? ResponseFormat { get; set; }
    }

    private sealed class ResponseFormatPayload
    {
        public string Type { get; set; } = "json_object";
    }

    private sealed class ChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private sealed class OpenAiChatCompletionResponse
    {
        public List<OpenAiChoice>? Choices { get; set; }
    }

    private sealed class OpenAiChoice
    {
        public ChatMessage? Message { get; set; }
    }

    /// <summary>Raw JSON contract returned by the model for the Improve Request flow.</summary>
    private sealed class IntakeAssistAiResponse
    {
        [JsonPropertyName("suggestedSummary")]
        public string? SuggestedSummary { get; set; }

        [JsonPropertyName("improvedDescription")]
        public string? ImprovedDescription { get; set; }

        [JsonPropertyName("missingDetails")]
        public List<string?>? MissingDetails { get; set; }

        [JsonPropertyName("clarityState")]
        public string? ClarityState { get; set; }

        [JsonPropertyName("guidanceMessage")]
        public string? GuidanceMessage { get; set; }
    }
}
