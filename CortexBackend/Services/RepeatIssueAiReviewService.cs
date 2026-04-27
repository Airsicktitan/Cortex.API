using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cortex.API.Configuration;
using Cortex.API.DTO;
using Microsoft.Extensions.Options;

namespace Cortex.API.Services;

/// <summary>
/// OpenAI-backed recurring issue reviewer. Patterned after <see cref="TicketTriageAiService"/>:
/// same <c>IAiSettingsService</c> governance, same retry/timeout helpers, same <c>Unavailable</c>
/// fallback semantics.
/// </summary>
public sealed class RepeatIssueAiReviewService : IRepeatIssueAiReviewService
{
    private const string OpenAiChatCompletionsUrl = "https://api.openai.com/v1/chat/completions";
    private const int FeatureMaxTokens = 900;

    private static readonly string[] AllowedCategories =
    {
        "Root-cause fix",
        "Automation",
        "Documentation",
        "Training",
        "Monitoring",
        "Process change",
    };

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly IAiSettingsService _aiSettingsService;
    private readonly IAiOutputSanitizer _sanitizer;
    private readonly ILogger<RepeatIssueAiReviewService> _logger;

    public RepeatIssueAiReviewService(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        IAiSettingsService aiSettingsService,
        ILogger<RepeatIssueAiReviewService> logger,
        IAiOutputSanitizer? sanitizer = null)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _aiSettingsService = aiSettingsService;
        _sanitizer = sanitizer ?? new AiOutputSanitizer();
        _logger = logger;
    }

    public async Task<RepeatIssueAiReviewResponse> GenerateReviewAsync(
        RepeatIssueAiReviewInput input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return Unavailable("AI review is not configured. Set OpenAI:ApiKey.");
        }

        var aiSettings = await _aiSettingsService.GetAsync();
        // Governance note: v1 reuses the triage umbrella flag for advisory text-AI features.
        // A dedicated IsRepeatIssueReviewEnabled flag can be added later without reshaping this service.
        if (!aiSettings.IsTriageEnabled)
        {
            return Unavailable("AI review is disabled by an administrator.");
        }

        if (string.IsNullOrWhiteSpace(aiSettings.DefaultTextModel))
        {
            return Unavailable("AI review is not configured. Set a default text model.");
        }

        var systemPrompt = BuildSystemPrompt();
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
                        "OpenAI repeat-issue review error. Attempt={Attempt} StatusCode={StatusCode} ReasonPhrase={ReasonPhrase} ResponseLength={ResponseLength}",
                        attempt + 1,
                        httpStatusCode,
                        reasonPhrase,
                        responseBody.Length);

                    if (attempt < aiSettings.RetryCount
                        && AiRequestExecution.ShouldRetry(response.StatusCode))
                    {
                        await Task.Delay(
                            AiRequestExecution.GetRetryDelay(attempt + 1),
                            cancellationToken);
                        continue;
                    }

                    return Unavailable("Unable to generate review at this time. Try again later.");
                }

                var outer = JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(
                    responseBody,
                    JsonSerializerOptions);
                var content = outer?.Choices?.FirstOrDefault()?.Message?.Content;
                if (string.IsNullOrWhiteSpace(content))
                {
                    return Unavailable("The model returned no review content.");
                }

                var model = JsonSerializer.Deserialize<ReviewAiModel>(content, JsonSerializerOptions);
                if (model is null)
                {
                    return Unavailable("Could not parse review response.");
                }

                return new RepeatIssueAiReviewResponse
                {
                    Summary = SanitizeSingleSentence(model.Summary),
                    Impact = SanitizeSingleSentence(model.Impact),
                    TrendCommentary = SanitizeSingleSentence(model.TrendCommentary),
                    CommonCharacteristics = SanitizeBullets(model.CommonCharacteristics, maxCount: 5),
                    SuggestedNextSteps = SanitizeSteps(model.SuggestedNextSteps),
                    Unavailable = false,
                };
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "OpenAI repeat-issue review timed out. Attempt={Attempt} TimeoutSeconds={TimeoutSeconds}",
                    attempt + 1,
                    aiSettings.TimeoutSeconds);

                if (attempt < aiSettings.RetryCount)
                {
                    await Task.Delay(
                        AiRequestExecution.GetRetryDelay(attempt + 1),
                        cancellationToken);
                    continue;
                }

                return Unavailable("Unable to generate review at this time. Try again later.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var httpRequestStatus =
                    ex is HttpRequestException httpEx
                        ? httpEx.StatusCode
                        : null;

                _logger.LogWarning(
                    ex,
                    "OpenAI repeat-issue review failed. Attempt={Attempt} HttpStatusCode={HttpStatusCode} ReasonPhrase={ReasonPhrase} ResponseLength={ResponseLength} HttpRequestExceptionStatusCode={HttpRequestExceptionStatusCode}",
                    attempt + 1,
                    httpStatusCode,
                    reasonPhrase,
                    responseBody?.Length ?? 0,
                    httpRequestStatus);

                if (attempt < aiSettings.RetryCount && ex is HttpRequestException httpException)
                {
                    if (AiRequestExecution.ShouldRetry(httpException.StatusCode))
                    {
                        await Task.Delay(
                            AiRequestExecution.GetRetryDelay(attempt + 1),
                            cancellationToken);
                        continue;
                    }
                }

                return Unavailable("Unable to generate review at this time. Try again later.");
            }
        }

        return Unavailable("Unable to generate review at this time. Try again later.");
    }

    private static string BuildSystemPrompt() => $"""
        You are an operational intelligence reviewer for CORTEX. You are given one recurring issue group —
        a cluster of tickets with similar signatures — and you must produce an executive-friendly advisory review.

        Output one JSON object only (no markdown). Fields:

        - summary: Exactly one sentence describing the recurring issue in plain language.
          Say what the recurring problem appears to be, based on the sample titles and signature tokens.
          Do not invent specific systems, people, teams, or root causes that are not named in the input.

        - impact: Exactly one sentence on the operational impact — repeated effort, open volume, touch count.
          Stay grounded in the numbers provided. Do not claim "hours lost by staff" or human work time;
          the supplied hours are lifecycle durations, not human work hours.

        - trendCommentary: Exactly one sentence on whether the pattern is rising, falling, or stable,
          based on the trendDelta and trendLabel supplied. Do not speculate beyond those numbers.

        - commonCharacteristics: JSON array of 2 to 5 short strings. Each is a single observation about
          what the tickets share (board, priority mix, typical status at resolution, signature language).
          Each bullet must be grounded in the supplied data.

        - suggestedNextSteps: JSON array of 2 to 4 objects. Each object has:
            - category: exactly one of: {string.Join(", ", AllowedCategories)}.
            - rationale: one concise sentence naming why that category fits this pattern.
          Prefer Root-cause fix, Automation, Documentation, Training, or Monitoring when any reasonably applies.
          Do not recommend actions that require information not in the input.

        Tone: confident, concise, executive-friendly. Do not hedge with "appears", "may", "might", or "probably".
        This is an advisory review — do not instruct the reader to take irreversible actions.
        """;

    private static string BuildUserPrompt(RepeatIssueAiReviewInput input)
    {
        var sb = new StringBuilder(2048);
        sb.AppendLine("## Recurring issue group");
        sb.AppendLine($"GroupKey: {input.GroupKey}");
        sb.AppendLine($"Representative title: {input.RepresentativeTitle}");
        sb.AppendLine($"Board: {input.BoardName}");
        sb.AppendLine($"Signature tokens: {string.Join(", ", input.SignatureTokens)}");
        sb.AppendLine($"Repeat count: {input.RepeatCount}");
        sb.AppendLine($"Open count: {input.OpenCount}");
        sb.AppendLine($"First seen: {input.FirstSeenUtc:yyyy-MM-dd}");
        sb.AppendLine($"Last seen: {input.LastSeenUtc:yyyy-MM-dd}");

        if (input.AvgResolutionHours.HasValue)
        {
            sb.AppendLine(
                $"Average resolution time (lifecycle duration, not human work time): {input.AvgResolutionHours.Value:F1} hours");
        }
        else
        {
            sb.AppendLine("Average resolution time: (no closed tickets in group)");
        }

        sb.AppendLine($"Total resolution time across group (lifecycle duration): {input.TotalResolutionHours:F1} hours");
        sb.AppendLine($"Operational touch count (comments across related tickets): {input.OperationalTouchCount}");
        sb.AppendLine($"30-day trend delta (last 30d minus prior 30d): {input.TrendDelta}");
        sb.AppendLine($"Trend label: {input.TrendLabel}");

        if (!string.IsNullOrWhiteSpace(input.DominantPriority))
        {
            sb.AppendLine($"Dominant priority: {input.DominantPriority}");
        }

        if (!string.IsNullOrWhiteSpace(input.DominantStatus))
        {
            sb.AppendLine($"Dominant current status: {input.DominantStatus}");
        }

        sb.AppendLine();
        sb.AppendLine("## Sample tickets");
        if (input.SampleTickets.Count == 0)
        {
            sb.AppendLine("(no samples supplied)");
        }
        else
        {
            foreach (var ticket in input.SampleTickets)
            {
                sb.Append("- ");
                sb.Append(ticket.Title);
                sb.Append($" [priority={ticket.Priority}, status={ticket.Status}");
                if (ticket.ResolutionHours.HasValue)
                {
                    sb.Append($", resolutionHours={ticket.ResolutionHours.Value:F1}");
                }

                sb.Append($", comments={ticket.CommentCount}, created={ticket.CreatedDate:yyyy-MM-dd}]");
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    private static RepeatIssueAiReviewResponse Unavailable(string reason) => new()
    {
        Unavailable = true,
        UnavailableReason = reason,
        CommonCharacteristics = [],
        SuggestedNextSteps = [],
    };

    private static string? NormalizeSingleSentence(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        var idx = trimmed.IndexOf(". ", StringComparison.Ordinal);
        return idx > 0 ? trimmed[..(idx + 1)].Trim() : trimmed;
    }

    private string? SanitizeSingleSentence(string? value)
    {
        return NormalizeSingleSentence(_sanitizer.Sanitize(value));
    }

    private List<string> SanitizeBullets(List<string>? items, int maxCount)
    {
        return (items ?? [])
            .Select(item => _sanitizer.Sanitize(item?.Trim()) ?? string.Empty)
            .Where(item => item.Length > 0)
            .Take(maxCount)
            .ToList();
    }

    private List<RepeatIssueSuggestedStep> SanitizeSteps(List<ReviewStepModel>? steps)
    {
        if (steps is null)
        {
            return [];
        }

        var result = new List<RepeatIssueSuggestedStep>();
        foreach (var step in steps.Take(4))
        {
            if (step is null)
            {
                continue;
            }

            var category = MatchAllowedCategory(step.Category);
            if (category is null)
            {
                continue;
            }

            var rationale = _sanitizer.Sanitize(step.Rationale?.Trim());
            if (string.IsNullOrWhiteSpace(rationale))
            {
                continue;
            }

            result.Add(new RepeatIssueSuggestedStep
            {
                Category = category,
                Rationale = rationale,
            });
        }

        return result;
    }

    private static string? MatchAllowedCategory(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var candidate = raw.Trim();
        foreach (var allowed in AllowedCategories)
        {
            if (string.Equals(candidate, allowed, StringComparison.OrdinalIgnoreCase))
            {
                return allowed;
            }
        }

        return null;
    }

    private sealed class ReviewAiModel
    {
        public string? Summary { get; set; }
        public string? Impact { get; set; }
        public string? TrendCommentary { get; set; }
        public List<string>? CommonCharacteristics { get; set; }
        public List<ReviewStepModel>? SuggestedNextSteps { get; set; }
    }

    private sealed class ReviewStepModel
    {
        public string? Category { get; set; }
        public string? Rationale { get; set; }
    }

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
}
