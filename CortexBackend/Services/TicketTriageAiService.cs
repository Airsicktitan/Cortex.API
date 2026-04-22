using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cortex.API.Configuration;
using Cortex.API.DTO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cortex.API.Services;

public sealed class TicketTriageAiService : ITicketTriageAiService
{
    private const string OpenAiChatCompletionsUrl = "https://api.openai.com/v1/chat/completions";
    private const int FeatureMaxTokens = 1300;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly IAiSettingsService _aiSettingsService;
    private readonly ILogger<TicketTriageAiService> _logger;

    public TicketTriageAiService(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        IAiSettingsService aiSettingsService,
        ILogger<TicketTriageAiService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _aiSettingsService = aiSettingsService;
        _logger = logger;
    }

    public async Task<TicketTriageGenerateResponse> GenerateTriageAsync(
        TicketTriageInput input,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return Unavailable("AI triage is not configured. Set OpenAI:ApiKey.");
        }

        var aiSettings = await _aiSettingsService.GetAsync();
        if (!aiSettings.IsTriageEnabled)
        {
            return Unavailable("AI triage is disabled by an administrator.");
        }

        if (string.IsNullOrWhiteSpace(aiSettings.DefaultTextModel))
        {
            return Unavailable("AI triage is not configured. Set a default text model.");
        }

        var vocab = input.Vocabulary;
        if (vocab.Priorities.Count == 0)
        {
            _logger.LogWarning(
                "AI triage skipped: no SLA priority vocabulary (empty SLA configuration).");
            return Unavailable(
                "AI triage is unavailable because no priority vocabulary is configured (SLA).");
        }

        if (vocab.Statuses.Count == 0)
        {
            _logger.LogWarning(
                "AI triage: no enabled ticket statuses in configuration; status recommendations will be omitted.");
        }

        var systemPrompt = BuildSystemPrompt(input);
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
                        "OpenAI triage error response. Attempt={Attempt} StatusCode={StatusCode} ReasonPhrase={ReasonPhrase} Body={ResponseBody}",
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

                    return Unavailable("Unable to generate triage at this time. Try again later.");
                }

                var outer = JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(
                    responseBody,
                    JsonSerializerOptions);
                var content = outer?.Choices?.FirstOrDefault()?.Message?.Content;
                if (string.IsNullOrWhiteSpace(content))
                {
                    return Unavailable("The model returned no triage content.");
                }

                var triage = JsonSerializer.Deserialize<TriageAiModel>(content, JsonSerializerOptions);
                if (triage is null)
                {
                    return Unavailable("Could not parse triage response.");
                }

                var allowPriorityRecommendation =
                    aiSettings.IsPriorityRecommendationEnabled
                    && aiSettings.AllowPriorityRecommendation;
                var allowStatusRecommendation =
                    aiSettings.IsStatusRecommendationEnabled
                    && aiSettings.AllowStatusRecommendation;

                var suggestedPriority = allowPriorityRecommendation
                    ? TryNormalizeToAllowedPriority(triage.SuggestedPriority, vocab)
                    : null;
                var suggestedStatus = allowStatusRecommendation
                    ? TryNormalizeToAllowedStatus(triage.SuggestedStatus, vocab)
                    : null;

                var suggestedCategory = CortexAiCategoryVocabulary.TryMatch(triage.RecommendedCategory);
                if (!string.IsNullOrWhiteSpace(triage.RecommendedCategory) && suggestedCategory is null)
                {
                    _logger.LogWarning(
                        "AI triage model returned recommendedCategory not in system vocabulary: {Raw}",
                        triage.RecommendedCategory.Trim());
                }

                var suggestedOwner = TryNormalizeToAllowedOwner(
                    triage.RecommendedOwnerUserId,
                    input.EligibleOwnerCandidates);

                return new TicketTriageGenerateResponse
                {
                    Summary = NormalizeSingleSentence(triage.Summary),
                    SuggestedPriority = suggestedPriority,
                    PriorityReason = allowPriorityRecommendation
                        ? NormalizeSingleSentence(triage.PriorityReason)
                        : null,
                    SuggestedStatus = suggestedStatus,
                    MissingDetails = NormalizeMissing(triage.MissingDetails),
                    PotentialSlaRisk = NormalizeSlaRiskTier(triage.PotentialSlaRisk),
                    SlaRiskReason = NormalizeSingleSentence(triage.SlaRiskReason),
                    SuggestedCategory = suggestedCategory,
                    SuggestedOwnerUserId = suggestedOwner,
                    Unavailable = false,
                };
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "OpenAI triage timed out. Attempt={Attempt} TimeoutSeconds={TimeoutSeconds}",
                    attempt + 1,
                    aiSettings.TimeoutSeconds);

                if (attempt < aiSettings.RetryCount)
                {
                    await Task.Delay(
                        AiRequestExecution.GetRetryDelay(attempt + 1),
                        cancellationToken);
                    continue;
                }

                return Unavailable("Unable to generate triage at this time. Try again later.");
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
                    "OpenAI triage request failed. Attempt={Attempt} ExceptionMessage={ExceptionMessage} HttpStatusCode={HttpStatusCode} ReasonPhrase={ReasonPhrase} ResponseBody={ResponseBody} HttpRequestExceptionStatusCode={HttpRequestExceptionStatusCode}",
                    attempt + 1,
                    ex.Message,
                    httpStatusCode,
                    reasonPhrase,
                    responseBody ?? "(not available)",
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

                return Unavailable("Unable to generate triage at this time. Try again later.");
            }
        }

        return Unavailable("Unable to generate triage at this time. Try again later.");
    }

    private string? TryNormalizeToAllowedPriority(string? raw, TicketTriageVocabularySnapshot vocab)
    {
        var configured = CortexAiAssessmentConstraintMapper.TryMatchConfiguredPriorityName(
            raw,
            vocab.Priorities);
        if (configured is not null)
        {
            return configured;
        }

        if (!string.IsNullOrWhiteSpace(raw))
        {
            var synonym = CortexAiAssessmentConstraintMapper.ResolvePrioritySynonym(raw, vocab.Priorities);
            if (synonym is not null)
            {
                _logger.LogWarning(
                    "AI triage suggestedPriority not in vocabulary; applied synonym mapping: {Raw} -> {Mapped}",
                    raw.Trim(),
                    synonym);
                return synonym;
            }

            _logger.LogWarning(
                "AI triage model returned suggestedPriority not in configured vocabulary: {Raw}",
                raw.Trim());
        }

        return null;
    }

    private static string? TryNormalizeToAllowedOwner(
        string? raw,
        IReadOnlyList<(string UserId, string DisplayName)>? candidates)
    {
        if (candidates is null || candidates.Count == 0 || string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        foreach (var (userId, _) in candidates)
        {
            if (string.Equals(userId, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return userId;
            }
        }

        return null;
    }

    private string? TryNormalizeToAllowedStatus(string? raw, TicketTriageVocabularySnapshot vocab)
    {
        if (vocab.Statuses.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(raw))
            {
                _logger.LogWarning(
                    "AI triage model returned suggestedStatus but no statuses are configured; ignoring: {Raw}",
                    raw.Trim());
            }

            return null;
        }

        var canonical = MatchCanonical(raw, vocab.Statuses.Select(status => status.Name).ToList());
        if (canonical is null && !string.IsNullOrWhiteSpace(raw))
        {
            _logger.LogWarning(
                "AI triage model returned suggestedStatus not in configured vocabulary: {Raw}",
                raw.Trim());
        }

        return canonical;
    }

    private static string? MatchCanonical(string? raw, IReadOnlyList<string> allowed)
    {
        var candidate = NormalizeWhitespace(raw);
        if (candidate is null)
        {
            return null;
        }

        foreach (var name in allowed)
        {
            if (string.Equals(candidate, name, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return null;
    }

    private static string BuildSystemPrompt(TicketTriageInput input)
    {
        var vocab = input.Vocabulary;
        var priorityNames = vocab.Priorities.Select(priority => priority.Name).ToList();
        var priorityFieldLine =
            $"- suggestedPriority: Exactly one of these Cortex-configured priority names (case-insensitive match allowed in reasoning; output the exact spelling from the list): {string.Join(", ", priorityNames)}.";

        string statusBlock;

        if (vocab.Statuses.Count > 0)
        {
            var statusNames = vocab.Statuses.Select(status => status.Name).ToList();
            statusBlock = $"""

                - suggestedStatus: Either null or exactly one of these Cortex-configured status names (same exact-spelling rule): {string.Join(", ", statusNames)}. Use null when no status change is justified. If the ticket does not justify a status change, use null. If multiple statuses seem plausible, prefer the most operationally accurate one using the definitions supplied in the user message.

                """;
        }
        else
        {
            statusBlock =
                "- suggestedStatus: Must be null or omitted (no status vocabulary is configured for this environment).";
        }

        return $"""
            You are a decisive intake assistant for CORTEX ticket reviewers. Output one JSON object only (no markdown).

            Ticket statuses and priorities are controlled by Cortex system configuration. They may vary by environment and may grow over time—there may be more valid values than in any prior description. Only use values from the lists supplied in the user message. Do not invent, assume, or normalize to values that are not explicitly supplied. Do not invent new statuses or priorities unless the prompt explicitly allows it (it does not).

            Tone: confident, concise, reviewer-first. No filler, no passive summarizing, no hedging. Do not use words like "appears", "likely", "seems", "may", "might", or "probably".

            Fields:

            - summary: Exactly one sentence. Rewrite the work as a direct statement of what is being asked—state the ask itself (imperative or tight declarative). Do not describe the ticket; do not open with "The requester", "This ticket", or similar filler.

            {priorityFieldLine}

            - priorityReason: Exactly one sentence. Direct justification focused on business impact. State why that tier fits; no hedging. If multiple priorities seem plausible, prefer the one most justified by impact, urgency, and the ticket text.

            {statusBlock}

            - missingDetails: A JSON array of 2 to 4 strings only. Each string is one short, imperative bullet naming a specific fact, scope boundary, owner, system, timeline, or approval criterion still needed. Every bullet must be actionable (e.g. "Confirm X with Y")—never use vague lines like "more information" or "additional details" without naming what is missing.

            - potentialSlaRisk: Exactly one of: Low, Medium, or High. Advisory only: how much delivery pressure or slower handling this work could create if it moves forward without enough clarification—judge from ticket clarity, implied urgency, likely complexity, dependency or handoff signals, and expected follow-up burden. This is not the same as ticket priority; it is an advisory risk tier. Do not output anything other than Low, Medium, or High.

            - slaRiskReason: Exactly one sentence. Explains the risk tier using only what is inferable from the ticket text and fields below. Do not predict breach times, dates, or calendar SLAs. Do not reference specific owners, teams, or workloads unless the ticket text explicitly names them. No hedging words.

            Rules:

            - Do not invent routing, board assignments, or SLA commitments.

            - Only recommend or set a priority that exists in the provided valid priority list. Only recommend or set a status that exists in the provided valid status list (when that list is non-empty).

            - If the ticket is thin or ambiguous, still pick a valid suggestedPriority decisively and use missingDetails to name exactly what must be clarified.

            - For potentialSlaRisk: thin or ambiguous intake raises risk; crisp, bounded asks with clear acceptance criteria lower it. Operational incidents or org-wide impact described in the ticket justify higher risk when the ask itself is still underspecified.

            Fusion intake fields (always include in JSON):

            - recommendedCategory: Exactly one of these system category labels, or an empty string "" when none applies: {string.Join(", ", CortexAiCategoryVocabulary.Values)}.

            - recommendedOwnerUserId: When the user message includes an "Eligible Syniti owner candidates" list with one or more entries, this must be exactly one of the listed user id strings, or null. When that list is absent or empty, this must be null.
            """;
    }

    private static string BuildUserPrompt(TicketTriageInput input)
    {
        var vocabulary = input.Vocabulary;
        var sb = new StringBuilder(2048);

        sb.AppendLine("## Ticket");
        sb.AppendLine($"Title: {input.Title}");
        sb.AppendLine("Description:");
        sb.AppendLine(input.Description);
        sb.AppendLine($"Current priority (requester): {input.CurrentPriority}");
        sb.AppendLine($"Current status: {input.Status}");
        sb.AppendLine($"Department: {input.Department ?? "(none)"}");
        sb.AppendLine($"Board: {input.BoardName}");
        sb.AppendLine();

        sb.AppendLine("## Valid priorities (controlled vocabulary from Cortex SLA configuration)");
        sb.AppendLine("Use suggestedPriority only from this list. Optional metadata describes policy timing, not business meaning.");
        foreach (var priority in vocabulary.Priorities)
        {
            sb.Append($"- {priority.Name} (target response: {priority.TargetHours}h, warning: {priority.WarningHours}h)");
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("## Valid statuses (controlled vocabulary from Cortex ticket status definitions)");
        if (vocabulary.Statuses.Count > 0)
        {
            sb.AppendLine(
                "Statuses are system-defined labels. Optional descriptions come from configuration and are hints only—not universal rules.");
            foreach (var status in vocabulary.Statuses.OrderBy(value => value.SortKey))
            {
                if (!string.IsNullOrWhiteSpace(status.Description))
                {
                    sb.AppendLine($"- {status.Name}: {status.Description}");
                }
                else
                {
                    sb.AppendLine($"- {status.Name}");
                }
            }

            var sequence = string.Join(
                " -> ",
                vocabulary.Statuses.OrderBy(value => value.SortKey).Select(value => value.Name));
            sb.AppendLine();
            sb.AppendLine("Typical ordering hint (informational only; workflows may skip steps):");
            sb.AppendLine(sequence);
        }
        else
        {
            sb.AppendLine("(No enabled statuses are configured; do not suggest a status.)");
        }

        if (!string.IsNullOrWhiteSpace(input.SupplementalContext))
        {
            sb.AppendLine();
            sb.AppendLine("## Fused context (comments, vision evidence, and other signals)");
            sb.AppendLine(input.SupplementalContext.Trim());
        }

        if (input.EligibleOwnerCandidates is { Count: > 0 })
        {
            sb.AppendLine();
            sb.AppendLine("## Eligible Syniti owner candidates (recommendedOwnerUserId must be one of these user ids or null)");
            foreach (var (userId, displayName) in input.EligibleOwnerCandidates)
            {
                sb.AppendLine($"- {userId} — {displayName}");
            }
        }

        return sb.ToString();
    }

    private static TicketTriageGenerateResponse Unavailable(string reason) =>
        new()
        {
            Unavailable = true,
            UnavailableReason = reason,
            MissingDetails = [],
        };

    private static string? NormalizeWhitespace(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private static string? NormalizeSingleSentence(string? value)
    {
        var trimmed = NormalizeWhitespace(value);
        if (trimmed is null)
        {
            return null;
        }

        var idx = trimmed.IndexOf(". ", StringComparison.Ordinal);
        if (idx > 0)
        {
            return trimmed[..(idx + 1)].Trim();
        }

        return trimmed;
    }

    private static string? NormalizeSlaRiskTier(string? raw)
    {
        var candidate = NormalizeWhitespace(raw);
        if (candidate is null)
        {
            return null;
        }

        foreach (var allowed in new[] { "High", "Medium", "Low" })
        {
            if (string.Equals(candidate, allowed, StringComparison.OrdinalIgnoreCase))
            {
                return allowed;
            }
        }

        return null;
    }

    private static List<string> NormalizeMissing(List<string>? items)
    {
        var list = (items ?? [])
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .Take(4)
            .ToList();

        var padding = new[]
        {
            "Name the system, data set, or business process in scope.",
            "State the expected outcome or acceptance criteria for approval.",
            "Identify the business owner or stakeholder for sign-off.",
        };

        foreach (var suggestion in padding)
        {
            if (list.Count >= 2)
            {
                break;
            }

            if (list.Any(item => string.Equals(item, suggestion, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            list.Add(suggestion);
        }

        return list;
    }

    private sealed class TriageAiModel
    {
        public string? Summary { get; set; }
        public string? SuggestedPriority { get; set; }
        public string? PriorityReason { get; set; }
        public string? SuggestedStatus { get; set; }
        public List<string>? MissingDetails { get; set; }
        public string? PotentialSlaRisk { get; set; }
        public string? SlaRiskReason { get; set; }

        [JsonPropertyName("recommendedCategory")]
        public string? RecommendedCategory { get; set; }

        [JsonPropertyName("recommendedOwnerUserId")]
        public string? RecommendedOwnerUserId { get; set; }
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
