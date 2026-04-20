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

public sealed class ScreenshotInsightAiService : IScreenshotInsightAiService
{
    private const string OpenAiChatCompletionsUrl = "https://api.openai.com/v1/chat/completions";
    private const int MaxSummaryLength = 1200;
    private const int MaxListItems = 8;
    private const int MaxBulletLength = 400;
    private const int FeatureMaxTokens = 1800;

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly IAiSettingsService _aiSettingsService;
    private readonly IScreenshotInsightPromptBuilder _promptBuilder;
    private readonly ILogger<ScreenshotInsightAiService> _logger;

    public ScreenshotInsightAiService(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        IAiSettingsService aiSettingsService,
        IScreenshotInsightPromptBuilder promptBuilder,
        ILogger<ScreenshotInsightAiService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _aiSettingsService = aiSettingsService;
        _promptBuilder = promptBuilder;
        _logger = logger;
    }

    public async Task<ScreenshotInsightResponse> AnalyzeAsync(
        string ticketTitle,
        IReadOnlyList<(string FileName, string ContentType, byte[] Content)> images,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return Unavailable("Screenshot insight is not configured. Set OpenAI:ApiKey.");
        }

        if (images.Count == 0)
        {
            return Unavailable("No images to analyze.");
        }

        var aiSettings = await _aiSettingsService.GetAsync();
        if (!aiSettings.IsScreenshotInsightEnabled)
        {
            return Unavailable("Screenshot insight is disabled by an administrator.");
        }

        if (string.IsNullOrWhiteSpace(aiSettings.DefaultVisionModel))
        {
            return Unavailable("Screenshot insight is not configured. Set a default vision model.");
        }

        var systemPrompt = _promptBuilder.BuildSystemPrompt();
        var names = images.Select(image => image.FileName).ToList();
        var userText = _promptBuilder.BuildUserIntro(ticketTitle, names);

        var userContentParts = new List<object>
        {
            new { type = "text", text = userText },
        };

        foreach (var image in images)
        {
            var mime = NormalizeImageMimeType(image.ContentType, image.FileName);
            var b64 = Convert.ToBase64String(image.Content);
            var dataUrl = $"data:{mime};base64,{b64}";
            userContentParts.Add(new
            {
                type = "image_url",
                image_url = new { url = dataUrl },
            });
        }

        var requestPayload = new
        {
            model = aiSettings.DefaultVisionModel.Trim(),
            temperature = aiSettings.Temperature,
            max_tokens = AiRequestExecution.ResolveMaxTokens(aiSettings.MaxTokens, FeatureMaxTokens),
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userContentParts },
            },
        };

        var json = JsonSerializer.Serialize(requestPayload, JsonSerializerOptions);

        for (var attempt = 0; attempt <= aiSettings.RetryCount; attempt++)
        {
            using var timeoutScope = AiRequestExecution.CreateTimeoutScope(
                cancellationToken,
                aiSettings.TimeoutSeconds);

            string? responseBody = null;
            HttpStatusCode? statusCode = null;

            try
            {
                using var httpRequest = new HttpRequestMessage(HttpMethod.Post, OpenAiChatCompletionsUrl);
                httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                httpRequest.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _options.ApiKey.Trim());
                httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(httpRequest, timeoutScope.Token);
                statusCode = response.StatusCode;
                responseBody = await response.Content.ReadAsStringAsync(timeoutScope.Token);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning(
                        "OpenAI screenshot-insight error. Attempt={Attempt} StatusCode={StatusCode} Body={Body}",
                        attempt + 1,
                        (int)response.StatusCode,
                        responseBody);

                    if (attempt < aiSettings.RetryCount
                        && AiRequestExecution.ShouldRetry(response.StatusCode))
                    {
                        await Task.Delay(
                            AiRequestExecution.GetRetryDelay(attempt + 1),
                            cancellationToken);
                        continue;
                    }

                    return Unavailable("Unable to analyze screenshots right now. Try again later.");
                }

                var outer = JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(
                    responseBody,
                    JsonSerializerOptions);
                var content = outer?.Choices?.FirstOrDefault()?.Message?.Content;
                if (string.IsNullOrWhiteSpace(content))
                {
                    return Unavailable("Unable to analyze screenshots right now. Try again later.");
                }

                ScreenshotInsightAiRaw? parsed;
                try
                {
                    parsed = JsonSerializer.Deserialize<ScreenshotInsightAiRaw>(
                        content,
                        JsonSerializerOptions);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Screenshot insight returned non-JSON. Content={Content}", content);
                    return Unavailable("Unable to analyze screenshots right now. Try again later.");
                }

                if (parsed is null)
                {
                    return Unavailable("Unable to analyze screenshots right now. Try again later.");
                }

                return Sanitize(parsed);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Screenshot insight timed out. Attempt={Attempt} TimeoutSeconds={TimeoutSeconds}",
                    attempt + 1,
                    aiSettings.TimeoutSeconds);

                if (attempt < aiSettings.RetryCount)
                {
                    await Task.Delay(
                        AiRequestExecution.GetRetryDelay(attempt + 1),
                        cancellationToken);
                    continue;
                }

                return Unavailable("Unable to analyze screenshots right now. Try again later.");
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Screenshot insight request failed. Attempt={Attempt} HttpStatusCode={HttpStatusCode}",
                    attempt + 1,
                    statusCode);

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

                return Unavailable("Unable to analyze screenshots right now. Try again later.");
            }
        }

        return Unavailable("Unable to analyze screenshots right now. Try again later.");
    }

    private static string NormalizeImageMimeType(string contentType, string fileName)
    {
        var normalizedContentType = contentType.Trim().ToLowerInvariant();
        var semicolon = normalizedContentType.IndexOf(';');
        if (semicolon >= 0)
        {
            normalizedContentType = normalizedContentType[..semicolon].Trim();
        }

        if (normalizedContentType is "image/jpg")
        {
            normalizedContentType = "image/jpeg";
        }

        if (normalizedContentType is "image/png" or "image/jpeg" or "image/webp")
        {
            return normalizedContentType;
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        return extension switch
        {
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".webp" => "image/webp",
            _ => "image/png",
        };
    }

    private static ScreenshotInsightResponse Sanitize(ScreenshotInsightAiRaw raw)
    {
        static List<string> CleanList(IReadOnlyList<string?>? list)
        {
            if (list is null || list.Count == 0)
            {
                return [];
            }

            return list
                .Select(value => value?.Trim())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => Truncate(value!, MaxBulletLength)!)
                .Take(MaxListItems)
                .ToList();
        }

        var summary = Truncate(raw.Summary?.Trim(), MaxSummaryLength) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(summary))
        {
            summary = "No summary was returned for these image(s).";
        }

        return new ScreenshotInsightResponse
        {
            Summary = summary,
            VisibleDetails = CleanList(raw.VisibleDetails),
            PossibleIssues = CleanList(raw.PossibleIssues),
            RecommendedFollowUp = CleanList(raw.RecommendedFollowUp),
            Unavailable = false,
        };
    }

    private static string? Truncate(string? value, int max)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return value.Length <= max ? value : value[..max].TrimEnd();
    }

    private static ScreenshotInsightResponse Unavailable(string reason) =>
        new()
        {
            Unavailable = true,
            UnavailableReason = reason,
        };

    private sealed class OpenAiChatCompletionResponse
    {
        public List<OpenAiChoice>? Choices { get; set; }
    }

    private sealed class OpenAiChoice
    {
        public OpenAiMessage? Message { get; set; }
    }

    private sealed class OpenAiMessage
    {
        public string? Content { get; set; }
    }

    private sealed class ScreenshotInsightAiRaw
    {
        [JsonPropertyName("summary")]
        public string? Summary { get; set; }

        [JsonPropertyName("visibleDetails")]
        public List<string?>? VisibleDetails { get; set; }

        [JsonPropertyName("possibleIssues")]
        public List<string?>? PossibleIssues { get; set; }

        [JsonPropertyName("recommendedFollowUp")]
        public List<string?>? RecommendedFollowUp { get; set; }
    }
}
