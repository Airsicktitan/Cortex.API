using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cortex.API.Configuration;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Cortex.API.Services;

/// <summary>
/// Lightweight ticket memory: deterministic keyword matching plus advisory AI summarization.
/// v1.5 intentionally avoids embeddings, vector storage, persistence, and automatic actions.
/// </summary>
public sealed class CortexInsightService : ICortexInsightService
{
    private const string OpenAiChatCompletionsUrl = "https://api.openai.com/v1/chat/completions";
    private const int MaxKeywords = 10;
    private const int CandidatePoolSize = 60;
    private const int MaxSimilarTickets = 3;
    private const int MinimumSimilarityScore = 25;
    private const int FeatureMaxTokens = 700;
    private const int PromptTextLimit = 700;
    private const int SourceQuoteLimit = 200;
    private const int SemanticCandidatePoolSize = 60;
    private const double SemanticHighConfidenceThreshold = 0.75;
    private const double SemanticMediumConfidenceThreshold = 0.50;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(7);

    private static readonly HashSet<string> TerminalStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        "Resolved",
        "Closed",
        "Done",
        "Completed",
        "Cancelled",
        "Canceled",
        "Rejected",
        "Archived",
    };

    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "for", "with", "from", "into", "onto", "that", "this", "these", "those",
        "about", "after", "before", "between", "during", "over", "under", "through",
        "please", "help", "need", "needs", "want", "wants", "would", "could", "should",
        "ticket", "tickets", "issue", "issues", "problem", "problems", "request", "requests",
        "error", "errors", "bug", "bugs", "fix", "fixing", "update", "updating",
        "new", "add", "adding", "remove", "removing", "change", "changes", "changing",
        "when", "where", "what", "which", "while", "since", "because",
        "user", "users", "team", "teams", "test", "testing", "tests",
    };

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly CortexDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly IAiSettingsService _aiSettingsService;
    private readonly IMemoryCache _cache;
    private readonly IAiOutputSanitizer _sanitizer;
    private readonly ILogger<CortexInsightService> _logger;
    private readonly ICortexMemoryFeedbackService _feedbackService;
    private readonly ICortexLearningService? _learningService;

    public CortexInsightService(
        CortexDbContext db,
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        IAiSettingsService aiSettingsService,
        IMemoryCache cache,
        ILogger<CortexInsightService> logger,
        ICortexMemoryFeedbackService feedbackService,
        ICortexLearningService? learningService = null,
        IAiOutputSanitizer? sanitizer = null)
    {
        _db = db;
        _httpClient = httpClient;
        _options = options.Value;
        _aiSettingsService = aiSettingsService;
        _cache = cache;
        _sanitizer = sanitizer ?? new AiOutputSanitizer();
        _logger = logger;
        _feedbackService = feedbackService;
        _learningService = learningService;
    }

    public bool TryGetCachedInsight(
        string ticketId,
        TicketVisibilityContext visibilityContext,
        out CortexInsightDto? insight)
    {
        insight = null;
        return !string.IsNullOrWhiteSpace(ticketId)
            && _cache.TryGetValue(BuildLatestCacheKey(ticketId, visibilityContext), out insight)
            && insight is not null;
    }

    public async Task<CortexInsightDto> GetInsightAsync(
        Ticket currentTicket,
        TicketVisibilityContext visibilityContext,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = await BuildCacheKeyAsync(currentTicket, visibilityContext, cancellationToken);
        var latestCacheKey = BuildLatestCacheKey(currentTicket.Id, visibilityContext);
        if (_cache.TryGetValue<CortexInsightDto>(cacheKey, out var cached) && cached is not null)
        {
            CacheInsight(latestCacheKey, cached);
            return cached;
        }

        var keywords = ExtractKeywords($"{currentTicket.Title} {currentTicket.Description}")
            .Take(MaxKeywords)
            .ToList();

        var semanticSimilarities = await TryGetSemanticSimilaritiesAsync(currentTicket.Id, cancellationToken);

        if (keywords.Count == 0 && semanticSimilarities.Count == 0)
        {
            var empty = Empty(currentTicket.Id);
            empty.LearningSignals = await GetLearningSignalsSafeAsync(
                currentTicket.Id,
                Array.Empty<string>(),
                cancellationToken);
            CacheInsight(cacheKey, latestCacheKey, empty);
            return empty;
        }

        var currentTitleTokens = Tokenize(currentTicket.Title);

        var keywordCandidates = keywords.Count > 0
            ? await _db.Tickets
                .AsNoTracking()
                .Include(ticket => ticket.Comments)
                .Where(ticket => ticket.Id != currentTicket.Id)
                .Where(BuildKeywordPredicate(keywords))
                .OrderByDescending(ticket => ticket.LastModifiedDate ?? ticket.CreatedDate)
                .Take(CandidatePoolSize)
                .ToListAsync(cancellationToken)
            : [];

        List<Ticket> semanticOnlyCandidates = [];
        if (semanticSimilarities.Count > 0)
        {
            var keywordIds = keywordCandidates.Select(ticket => ticket.Id).ToHashSet();
            var semanticOnlyIds = semanticSimilarities
                .Where(pair => pair.Value >= SemanticMediumConfidenceThreshold)
                .OrderByDescending(pair => pair.Value)
                .Take(SemanticCandidatePoolSize)
                .Select(pair => pair.Key)
                .Where(id => !keywordIds.Contains(id))
                .ToList();

            if (semanticOnlyIds.Count > 0)
            {
                semanticOnlyCandidates = await _db.Tickets
                    .AsNoTracking()
                    .Include(ticket => ticket.Comments)
                    .Where(ticket => semanticOnlyIds.Contains(ticket.Id))
                    .ToListAsync(cancellationToken);
            }
        }

        var candidates = keywordCandidates.Concat(semanticOnlyCandidates).ToList();

        var requesterIds = candidates
            .Select(ticket => ticket.CreatedBy)
            .Append(currentTicket.CreatedBy)
            .Distinct()
            .ToList();
        var departmentsByUserId = requesterIds.Count == 0
            ? new Dictionary<int, string?>()
            : await _db.Users
                .AsNoTracking()
                .Where(user => requesterIds.Contains(user.Id))
                .ToDictionaryAsync(user => user.Id, user => user.Department, cancellationToken);
        departmentsByUserId.TryGetValue(currentTicket.CreatedBy, out var currentDepartment);

        var matches = candidates
            .Where(visibilityContext.CanView)
            .Select(ticket =>
            {
                departmentsByUserId.TryGetValue(ticket.CreatedBy, out var candidateDepartment);
                semanticSimilarities.TryGetValue(ticket.Id, out var semanticSim);
                return ToMatch(ticket, currentTitleTokens, keywords, currentDepartment, candidateDepartment, semanticSim);
            })
            .Where(ticket => ticket.ConfidenceScore >= MinimumSimilarityScore)
            .OrderByDescending(ticket => ticket.ConfidenceScore)
            .ThenByDescending(ticket => ticket.LastModifiedDate ?? ticket.CreatedDate)
            .Take(MaxSimilarTickets)
            .ToList();

        if (matches.Count == 0)
        {
            var empty = Empty(currentTicket.Id);
            empty.LearningSignals = await GetLearningSignalsSafeAsync(
                currentTicket.Id,
                Array.Empty<string>(),
                cancellationToken);
            CacheInsight(cacheKey, latestCacheKey, empty);
            return empty;
        }

        foreach (var match in matches)
        {
            await _feedbackService.RecordAsync(
                ticketId: currentTicket.Id,
                eventType: CortexMemoryEventType.RelatedTicketShown,
                source: "CortexInsight",
                relatedTicketId: match.Id,
                createdByUserId: visibilityContext.UserId,
                createdByDisplayName: visibilityContext.DisplayName,
                metadataJson: $"{{\"confidenceScore\":{match.ConfidenceScore}}}",
                cancellationToken: cancellationToken);
        }

        var insight = await GenerateInsightAsync(currentTicket, matches, cancellationToken);
        var displayedIds = matches.Select(m => m.Id).ToArray();
        insight.LearningSignals = await GetLearningSignalsSafeAsync(
            currentTicket.Id,
            displayedIds,
            cancellationToken);
        CacheInsight(cacheKey, latestCacheKey, insight);
        return insight;
    }

    private async Task<List<CortexLearningSignalDto>> GetLearningSignalsSafeAsync(
        string ticketId,
        IReadOnlyCollection<string> displayedSimilarTicketIds,
        CancellationToken cancellationToken)
    {
        if (_learningService is null)
        {
            return [];
        }

        try
        {
            return await _learningService.GetLearningSignalsAsync(
                ticketId,
                displayedSimilarTicketIds,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Cortex learning signals unavailable for ticket {TicketId}.",
                ticketId);
            return [];
        }
    }

    public async Task<CortexInsightDto> GenerateInsightAsync(
        Ticket currentTicket,
        IReadOnlyList<CortexInsightSimilarTicketDto> similarTickets,
        CancellationToken cancellationToken = default)
    {
        var baseResponse = new CortexInsightDto
        {
            TicketId = currentTicket.Id,
            Matches = similarTickets.ToList(),
            ConfidenceScore = ComputeOverallConfidence(similarTickets),
            MatchReasons = BuildAggregateReasons(similarTickets),
        };

        if (similarTickets.Count == 0)
        {
            return baseResponse;
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return Unavailable(baseResponse, "Cortex Insight is not configured. Set OpenAI:ApiKey.");
        }

        var aiSettings = await _aiSettingsService.GetAsync();
        // Governance note: v1 reuses the triage umbrella flag for advisory text-AI features.
        if (!aiSettings.IsTriageEnabled)
        {
            return Unavailable(baseResponse, "Cortex Insight is disabled by an administrator.");
        }

        if (string.IsNullOrWhiteSpace(aiSettings.DefaultTextModel))
        {
            return Unavailable(baseResponse, "Cortex Insight is not configured. Set a default text model.");
        }

        var requestBody = new OpenAiChatRequest
        {
            Model = aiSettings.DefaultTextModel.Trim(),
            Messages =
            [
                new ChatMessage { Role = "system", Content = BuildSystemPrompt() },
                new ChatMessage { Role = "user", Content = BuildUserPrompt(currentTicket, similarTickets) },
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
                        "OpenAI Cortex Insight error. Attempt={Attempt} StatusCode={StatusCode} ReasonPhrase={ReasonPhrase} ResponseLength={ResponseLength}",
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

                    return Unavailable(baseResponse, "Unable to generate Cortex Insight at this time. Try again later.");
                }

                var outer = JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(
                    responseBody,
                    JsonSerializerOptions);
                var content = outer?.Choices?.FirstOrDefault()?.Message?.Content;
                if (string.IsNullOrWhiteSpace(content))
                {
                    return Unavailable(baseResponse, "The model returned no Cortex Insight content.");
                }

                var model = JsonSerializer.Deserialize<CortexInsightAiModel>(content, JsonSerializerOptions);
                if (model is null)
                {
                    return Unavailable(baseResponse, "Could not parse Cortex Insight response.");
                }

                baseResponse.Summary = SanitizeText(model.Summary);
                baseResponse.Resolution = SanitizeText(model.Resolution);
                baseResponse.RootCause = SanitizeText(model.RootCause);
                baseResponse.SuggestedNextStep = SanitizeText(model.SuggestedNextStep);
                return baseResponse;
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "OpenAI Cortex Insight timed out. Attempt={Attempt} TimeoutSeconds={TimeoutSeconds}",
                    attempt + 1,
                    aiSettings.TimeoutSeconds);

                if (attempt < aiSettings.RetryCount)
                {
                    await Task.Delay(
                        AiRequestExecution.GetRetryDelay(attempt + 1),
                        cancellationToken);
                    continue;
                }

                return Unavailable(baseResponse, "Unable to generate Cortex Insight at this time. Try again later.");
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
                    "OpenAI Cortex Insight failed. Attempt={Attempt} HttpStatusCode={HttpStatusCode} ReasonPhrase={ReasonPhrase} ResponseLength={ResponseLength} HttpRequestExceptionStatusCode={HttpRequestExceptionStatusCode}",
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

                return Unavailable(baseResponse, "Unable to generate Cortex Insight at this time. Try again later.");
            }
        }

        return Unavailable(baseResponse, "Unable to generate Cortex Insight at this time. Try again later.");
    }

    private async Task<string> BuildCacheKeyAsync(
        Ticket currentTicket,
        TicketVisibilityContext visibilityContext,
        CancellationToken cancellationToken)
    {
        var ticketRevision = await _db.Tickets
            .AsNoTracking()
            .Select(ticket => (DateTime?)(ticket.LastModifiedDate ?? ticket.CreatedDate))
            .MaxAsync(cancellationToken)
            ?? DateTime.MinValue;

        var commentRevision = await _db.Comments
            .AsNoTracking()
            .Select(comment => (DateTime?)comment.LastModifiedDate)
            .MaxAsync(cancellationToken)
            ?? DateTime.MinValue;

        var revisionTicks = Math.Max(ticketRevision.Ticks, commentRevision.Ticks);
        var currentFingerprint = HashForCache(
            $"{currentTicket.Title}\n{currentTicket.Description}\n{currentTicket.Status}\n{currentTicket.LastModifiedDate?.Ticks}");

        return string.Join(
            ':',
            "cortex-insight",
            currentTicket.Id,
            visibilityContext.UserId,
            visibilityContext.Scope,
            revisionTicks,
            currentFingerprint);
    }

    private static string BuildLatestCacheKey(
        string ticketId,
        TicketVisibilityContext visibilityContext) =>
        string.Join(
            ':',
            "cortex-insight-latest",
            ticketId,
            visibilityContext.UserId,
            visibilityContext.Scope);

    private void CacheInsight(string cacheKey, string latestCacheKey, CortexInsightDto insight)
    {
        _cache.Set(cacheKey, insight, CacheDuration);
        CacheInsight(latestCacheKey, insight);
    }

    private void CacheInsight(string cacheKey, CortexInsightDto insight) =>
        _cache.Set(cacheKey, insight, CacheDuration);

    private static string HashForCache(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes[..8]);
    }

    private static CortexInsightDto Empty(string ticketId) => new()
    {
        TicketId = ticketId,
        Matches = [],
        ConfidenceScore = 0,
        MatchReasons = [],
    };

    private static CortexInsightDto Unavailable(CortexInsightDto response, string reason)
    {
        response.Unavailable = true;
        response.UnavailableReason = reason;
        return response;
    }

    private static CortexInsightSimilarTicketDto ToMatch(
        Ticket ticket,
        IReadOnlyList<string> currentTitleTokens,
        IReadOnlyList<string> keywords,
        string? currentDepartment,
        string? candidateDepartment,
        double semanticSimilarity = 0.0)
    {
        var sourceQuote = ticket.Comments
            .Where(comment => !string.IsNullOrWhiteSpace(comment.Body))
            .OrderByDescending(comment => comment.LastModifiedDate)
            .ThenByDescending(comment => comment.CreatedDate)
            .Select(comment => Snippet(comment.Body, SourceQuoteLimit))
            .FirstOrDefault();

        var (keywordScore, reasons) = Score(
            ticket,
            currentTitleTokens,
            keywords,
            currentDepartment,
            candidateDepartment);

        int finalScore;
        if (semanticSimilarity > 0)
        {
            var semanticPts = (int)Math.Round(semanticSimilarity * 100.0);
            finalScore = Math.Clamp((int)Math.Round(semanticPts * 0.65 + keywordScore * 0.35), 0, 100);

            if (semanticSimilarity >= SemanticHighConfidenceThreshold)
                reasons.Insert(0, "Semantically similar to this ticket's request");
            else if (semanticSimilarity >= SemanticMediumConfidenceThreshold)
                reasons.Insert(0, "Shares historical pattern with prior ticket");
        }
        else
        {
            finalScore = keywordScore;
        }

        return new CortexInsightSimilarTicketDto
        {
            Id = ticket.Id,
            SourceTicketId = ticket.Id,
            SourceUrl = $"/tickets/{Uri.EscapeDataString(ticket.Id)}",
            Title = ticket.Title,
            Description = ticket.Description,
            Status = ticket.Status,
            LastMeaningfulComment = sourceQuote,
            SourceQuote = sourceQuote,
            CreatedDate = ticket.CreatedDate,
            LastModifiedDate = ticket.LastModifiedDate,
            SimilarityScore = finalScore,
            ConfidenceScore = finalScore,
            MatchReasons = reasons,
        };
    }

    private static (int Score, List<string> Reasons) Score(
        Ticket ticket,
        IReadOnlyList<string> currentTitleTokens,
        IReadOnlyList<string> keywords,
        string? currentDepartment,
        string? candidateDepartment)
    {
        var title = (ticket.Title ?? string.Empty).ToLowerInvariant();
        var description = (ticket.Description ?? string.Empty).ToLowerInvariant();
        var candidateTitleTokens = Tokenize(ticket.Title);
        var score = 0;
        var reasons = new List<string>();

        var titleOverlap = currentTitleTokens
            .Intersect(candidateTitleTokens, StringComparer.Ordinal)
            .ToList();
        if (titleOverlap.Count > 0)
        {
            var denominator = Math.Max(currentTitleTokens.Count, 1);
            var titleScore = (int)Math.Round(Math.Min(1d, titleOverlap.Count / (double)denominator) * 40);
            score += titleScore;
            reasons.Add($"Title terms match: {string.Join(", ", titleOverlap.Take(3))}");
        }

        var keywordHits = new List<string>();
        var keywordScore = 0;
        foreach (var keyword in keywords)
        {
            var hitTitle = title.Contains(keyword, StringComparison.Ordinal);
            var hitDescription = description.Contains(keyword, StringComparison.Ordinal);
            if (!hitTitle && !hitDescription)
            {
                continue;
            }

            keywordHits.Add(keyword);
            keywordScore += hitTitle ? 4 : 0;
            keywordScore += hitDescription ? 2 : 0;
        }

        if (keywordHits.Count > 0)
        {
            score += Math.Min(25, keywordScore);
            reasons.Add($"Shared keywords: {string.Join(", ", keywordHits.Take(4))}");
        }

        if (!string.IsNullOrWhiteSpace(currentDepartment)
            && string.Equals(
                currentDepartment.Trim(),
                candidateDepartment?.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
            reasons.Add($"Requester department matches {currentDepartment.Trim()}");
        }

        if (IsTerminalStatus(ticket.Status))
        {
            score += 12;
            reasons.Add($"Prior ticket is {ticket.Status}");
        }

        var activityDate = ticket.LastModifiedDate ?? ticket.CreatedDate;
        var ageDays = Math.Max(0, (DateTime.UtcNow - activityDate).TotalDays);
        var recencyScore = ageDays switch
        {
            <= 30 => 13,
            <= 90 => 9,
            <= 180 => 6,
            <= 365 => 3,
            _ => 0,
        };
        if (recencyScore > 0)
        {
            score += recencyScore;
            reasons.Add(ageDays <= 30
                ? "Recent activity within 30 days"
                : "Recent enough to compare");
        }

        return (Math.Clamp(score, 0, 100), reasons);
    }

    private static int ComputeOverallConfidence(IReadOnlyList<CortexInsightSimilarTicketDto> matches)
    {
        if (matches.Count == 0)
        {
            return 0;
        }

        var best = matches.Max(match => match.ConfidenceScore);
        var average = matches.Average(match => match.ConfidenceScore);
        return Math.Clamp((int)Math.Round(best * 0.65 + average * 0.35), 0, 100);
    }

    private static List<string> BuildAggregateReasons(IReadOnlyList<CortexInsightSimilarTicketDto> matches)
    {
        return matches
            .SelectMany(match => match.MatchReasons)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
    }

    private static bool IsTerminalStatus(string? status) =>
        !string.IsNullOrWhiteSpace(status) && TerminalStatuses.Contains(status.Trim());

    private static List<string> ExtractKeywords(string? text)
    {
        return Tokenize(text)
            .GroupBy(token => token, StringComparer.Ordinal)
            .OrderByDescending(group => group.Count())
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => group.Key)
            .ToList();
    }

    private static List<string> Tokenize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            builder.Append(char.IsLetterOrDigit(character) ? char.ToLowerInvariant(character) : ' ');
        }

        return builder
            .ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(token => token.Length >= 4)
            .Where(token => !Stopwords.Contains(token))
            .ToList();
    }

    private static Expression<Func<Ticket, bool>> BuildKeywordPredicate(IReadOnlyList<string> keywords)
    {
        Expression<Func<Ticket, bool>> predicate = ticket => false;
        foreach (var keyword in keywords)
        {
            var captured = keyword;
            Expression<Func<Ticket, bool>> term = ticket =>
                ticket.Title.ToLower().Contains(captured)
                || ticket.Description.ToLower().Contains(captured);
            predicate = Or(predicate, term);
        }

        return predicate;
    }

    private static Expression<Func<T, bool>> Or<T>(
        Expression<Func<T, bool>> left,
        Expression<Func<T, bool>> right)
    {
        var parameter = left.Parameters[0];
        var rewrittenRight = new ReplaceParameterVisitor(right.Parameters[0], parameter).Visit(right.Body)
            ?? throw new InvalidOperationException("Could not build Cortex Insight keyword predicate.");
        return Expression.Lambda<Func<T, bool>>(
            Expression.OrElse(left.Body, rewrittenRight),
            parameter);
    }

    private sealed class ReplaceParameterVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _source;
        private readonly ParameterExpression _target;

        public ReplaceParameterVisitor(ParameterExpression source, ParameterExpression target)
        {
            _source = source;
            _target = target;
        }

        protected override Expression VisitParameter(ParameterExpression node) =>
            node == _source ? _target : node;
    }

    private static string BuildSystemPrompt() => """
        You are Cortex Insight. Output JSON only with summary, resolution, rootCause, suggestedNextStep.
        Max 4 concise lines total across those fields; no hedging words; no markdown.
        Use only supplied source IDs, statuses, and quotes. Do not invent systems, owners, causes, or dates.
        suggestedNextStep must be a direct action; never recommend ownership changes.
        """;

    private static string BuildUserPrompt(
        Ticket currentTicket,
        IReadOnlyList<CortexInsightSimilarTicketDto> similarTickets)
    {
        var sb = new StringBuilder(2048);
        sb.AppendLine("## Current ticket");
        sb.AppendLine($"Id: {currentTicket.Id}");
        sb.AppendLine($"Title: {Truncate(currentTicket.Title, PromptTextLimit)}");
        sb.AppendLine($"Description: {Truncate(currentTicket.Description, PromptTextLimit)}");
        sb.AppendLine();
        sb.AppendLine("## Sources");

        foreach (var ticket in similarTickets)
        {
            sb.AppendLine($"- SourceTicketId: {ticket.SourceTicketId}");
            sb.AppendLine($"  SourceLink: {ticket.SourceUrl}");
            sb.AppendLine($"  Title: {Truncate(ticket.Title, PromptTextLimit)}");
            sb.AppendLine($"  Status: {ticket.Status}");
            sb.AppendLine($"  MatchConfidence: {ticket.ConfidenceScore}");
            sb.AppendLine($"  MatchReasons: {string.Join("; ", ticket.MatchReasons)}");
            sb.AppendLine($"  SourceQuote: {ticket.SourceQuote ?? "(none)"}");
        }

        return sb.ToString();
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : maxLength <= 3
                ? trimmed[..maxLength]
                : $"{trimmed[..(maxLength - 3)]}...";
    }

    private static string Snippet(string? value, int maxLength)
    {
        var trimmed = Truncate(value, maxLength).ReplaceLineEndings(" ");
        while (trimmed.Contains("  ", StringComparison.Ordinal))
        {
            trimmed = trimmed.Replace("  ", " ", StringComparison.Ordinal);
        }

        return trimmed;
    }

    private static string? NormalizeText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim();
    }

    private string? SanitizeText(string? value)
    {
        return NormalizeText(_sanitizer.Sanitize(value));
    }

    private static float[]? TryParseVector(string? vectorJson)
    {
        if (string.IsNullOrWhiteSpace(vectorJson))
            return null;
        var trimmed = vectorJson.Trim();
        if (string.Equals(trimmed, "[]", StringComparison.Ordinal))
            return null;
        try
        {
            var result = JsonSerializer.Deserialize<float[]>(trimmed);
            return result is { Length: > 0 } ? result : null;
        }
        catch
        {
            return null;
        }
    }

    private static double CosineSimilarity(float[] a, float[] b)
    {
        if (a.Length != b.Length)
            return 0.0;
        double dot = 0, magA = 0, magB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += (double)a[i] * b[i];
            magA += (double)a[i] * a[i];
            magB += (double)b[i] * b[i];
        }
        if (magA <= 0 || magB <= 0)
            return 0.0;
        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }

    private async Task<Dictionary<string, double>> TryGetSemanticSimilaritiesAsync(
        string currentTicketId,
        CancellationToken cancellationToken)
    {
        try
        {
            var currentEmbedding = await _db.TicketEmbeddings
                .AsNoTracking()
                .Where(e => e.TicketId == currentTicketId)
                .OrderByDescending(e => e.UpdatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            if (currentEmbedding is null)
                return [];

            var currentVector = TryParseVector(currentEmbedding.VectorJson);
            if (currentVector is null)
                return [];

            var otherEmbeddings = await _db.TicketEmbeddings
                .AsNoTracking()
                .Where(e => e.TicketId != currentTicketId)
                .ToListAsync(cancellationToken);

            var similarities = new Dictionary<string, double>(otherEmbeddings.Count);
            foreach (var embedding in otherEmbeddings)
            {
                var vector = TryParseVector(embedding.VectorJson);
                if (vector is null)
                    continue;

                var sim = CosineSimilarity(currentVector, vector);
                if (!similarities.TryGetValue(embedding.TicketId, out var existing) || sim > existing)
                    similarities[embedding.TicketId] = sim;
            }

            return similarities;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Cortex Memory semantic similarity failed for ticket {TicketId}. Falling back to keyword matching.",
                currentTicketId);
            return [];
        }
    }

    private sealed class CortexInsightAiModel
    {
        public string? Summary { get; set; }
        public string? Resolution { get; set; }
        public string? RootCause { get; set; }
        public string? SuggestedNextStep { get; set; }
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
