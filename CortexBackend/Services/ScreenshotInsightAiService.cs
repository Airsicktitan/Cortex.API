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

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly IScreenshotInsightPromptBuilder _promptBuilder;
    private readonly ILogger<ScreenshotInsightAiService> _logger;

    public ScreenshotInsightAiService(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        IScreenshotInsightPromptBuilder promptBuilder,
        ILogger<ScreenshotInsightAiService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _promptBuilder = promptBuilder;
        _logger = logger;
    }

    public async Task<ScreenshotInsightResponse> AnalyzeAsync(
        string ticketTitle,
        IReadOnlyList<(string FileName, string ContentType, byte[] Content)> images,
        CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
        {
            return Unavailable("Screenshot insight is not configured. Set OpenAI:ApiKey and OpenAI:Model.");
        }

        if (images.Count == 0)
        {
            return Unavailable("No images to analyze.");
        }

        var systemPrompt = _promptBuilder.BuildSystemPrompt();
        var names = images.Select(i => i.FileName).ToList();
        var userText = _promptBuilder.BuildUserIntro(ticketTitle, names);

        var userContentParts = new List<object>
        {
            new { type = "text", text = userText },
        };

        foreach (var img in images)
        {
            var mime = NormalizeImageMimeType(img.ContentType, img.FileName);
            var b64 = Convert.ToBase64String(img.Content);
            var dataUrl = $"data:{mime};base64,{b64}";
            userContentParts.Add(new
            {
                type = "image_url",
                image_url = new { url = dataUrl },
            });
        }

        var requestPayload = new
        {
            model = _options.Model!.Trim(),
            temperature = 0.2,
            max_tokens = 1800,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userContentParts },
            },
        };

        var json = JsonSerializer.Serialize(requestPayload, JsonSerializerOptions);

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, OpenAiChatCompletionsUrl);
            httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            httpRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _options.ApiKey!.Trim());
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OpenAI screenshot-insight error. StatusCode={StatusCode} Body={Body}",
                    (int)response.StatusCode,
                    responseBody);
                return Unavailable("Unable to analyze screenshots right now. Try again later.");
            }

            var outer = JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(responseBody, JsonSerializerOptions);
            var content = outer?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                return Unavailable("Unable to analyze screenshots right now. Try again later.");
            }

            ScreenshotInsightAiRaw? parsed;
            try
            {
                parsed = JsonSerializer.Deserialize<ScreenshotInsightAiRaw>(content, JsonSerializerOptions);
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
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Screenshot insight request failed.");
            return Unavailable("Unable to analyze screenshots right now. Try again later.");
        }
    }

    private static string NormalizeImageMimeType(string contentType, string fileName)
    {
        var ct = contentType.Trim().ToLowerInvariant();
        var semicolon = ct.IndexOf(';');
        if (semicolon >= 0)
        {
            ct = ct[..semicolon].Trim();
        }

        if (ct is "image/jpg")
        {
            ct = "image/jpeg";
        }

        if (ct is "image/png" or "image/jpeg" or "image/webp")
        {
            return ct;
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext switch
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
                .Select(s => s?.Trim())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => Truncate(s!, MaxBulletLength)!)
                .Take(MaxListItems)
                .ToList();
        }

        var summary = Truncate(raw.Summary?.Trim(), MaxSummaryLength) ?? "";
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
