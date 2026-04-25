using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cortex.API.Configuration;
using Cortex.API.Models;
using Microsoft.Extensions.Options;

namespace Cortex.API.Services;

public sealed class RebalanceAiAdvisoryService(
    HttpClient httpClient,
    IOptions<OpenAiOptions> options,
    ILogger<RebalanceAiAdvisoryService> logger) : IRebalanceAiAdvisoryService
{
    private const string OpenAiChatCompletionsUrl = "https://api.openai.com/v1/chat/completions";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly OpenAiOptions _options = options.Value;

    public async Task<IReadOnlyDictionary<string, RebalanceAiAdvisory>> GenerateAdvisoriesAsync(
        IReadOnlyList<RebalanceAiDecisionPacket> packets,
        CancellationToken cancellationToken = default)
    {
        if (packets.Count == 0
            || !_options.EnableRebalanceAdvisory
            || !_options.IsConfigured)
        {
            return new Dictionary<string, RebalanceAiAdvisory>(StringComparer.OrdinalIgnoreCase);
        }

        var knownTicketIds = packets
            .Select(packet => packet.TicketId)
            .Where(ticketId => !string.IsNullOrWhiteSpace(ticketId))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (knownTicketIds.Count == 0)
        {
            return new Dictionary<string, RebalanceAiAdvisory>(StringComparer.OrdinalIgnoreCase);
        }

        var requestBody = new OpenAiChatRequest
        {
            Model = _options.Model!,
            Temperature = 0.2m,
            MaxTokens = 900,
            Messages =
            [
                new OpenAiChatMessage
                {
                    Role = "system",
                    Content = BuildSystemPrompt()
                },
                new OpenAiChatMessage
                {
                    Role = "user",
                    Content = BuildUserPrompt(packets)
                }
            ],
            ResponseFormat = new OpenAiResponseFormat { Type = "json_object" },
        };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, OpenAiChatCompletionsUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            request.Content = new StringContent(
                JsonSerializer.Serialize(requestBody, JsonSerializerOptions),
                Encoding.UTF8,
                "application/json");

            using var response = await httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "OpenAI rebalance advisory error response. StatusCode={StatusCode} ReasonPhrase={ReasonPhrase} Body={ResponseBody}",
                    (int)response.StatusCode,
                    response.ReasonPhrase,
                    responseBody);
                return new Dictionary<string, RebalanceAiAdvisory>(StringComparer.OrdinalIgnoreCase);
            }

            var outer = JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(
                responseBody,
                JsonSerializerOptions);
            var content = outer?.Choices?.FirstOrDefault()?.Message?.Content;
            if (string.IsNullOrWhiteSpace(content))
            {
                return new Dictionary<string, RebalanceAiAdvisory>(StringComparer.OrdinalIgnoreCase);
            }

            var envelope = JsonSerializer.Deserialize<RebalanceAdvisoryEnvelope>(
                content,
                JsonSerializerOptions);
            if (envelope?.Suggestions is null || envelope.Suggestions.Count == 0)
            {
                return new Dictionary<string, RebalanceAiAdvisory>(StringComparer.OrdinalIgnoreCase);
            }

            var result = new Dictionary<string, RebalanceAiAdvisory>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in envelope.Suggestions)
            {
                if (string.IsNullOrWhiteSpace(item.TicketId)
                    || !knownTicketIds.Contains(item.TicketId))
                {
                    continue;
                }

                result[item.TicketId] = new RebalanceAiAdvisory
                {
                    TicketId = item.TicketId,
                    Rationale = CleanOneLine(item.Rationale),
                    RiskSummary = CleanOneLine(item.RiskSummary),
                    TradeoffSummary = CleanOneLine(item.TradeoffSummary),
                    ConfidenceWording = CleanOneLine(item.ConfidenceWording),
                };
            }

            return result;
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "OpenAI rebalance advisory failed.");
            return new Dictionary<string, RebalanceAiAdvisory>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string BuildSystemPrompt() =>
        """
        You are Cortex Rebalance advisory language. Deterministic Cortex rules already selected the final owner.
        Output one JSON object only with a suggestions array.

        Rules:
        - Advisory only: do not assign owners, change statuses, change priorities, or override deterministic rules.
        - Use only the candidates and owners in the packet. Do not invent people, teams, rules, dates, or hidden policies.
        - Explain why the deterministic move helps, why the selected owner won, and what tradeoff remains.
        - If diversificationApplied is true, explain that Cortex avoided concentrating new recommendations on one owner.
        - If diversificationApplied is false, do not claim diversification happened.
        - Keep language concise, decisive, and product-facing. No hedging words.

        JSON shape:
        {
          "suggestions": [
            {
              "ticketId": "same id from packet",
              "rationale": "one sentence",
              "riskSummary": "one sentence",
              "tradeoffSummary": "one sentence",
              "confidenceWording": "one short phrase"
            }
          ]
        }
        """;

    private static string BuildUserPrompt(IReadOnlyList<RebalanceAiDecisionPacket> packets) =>
        $$"""
        Build advisory language for these deterministic rebalance decisions.
        Do not change finalCandidateName or selectedOwner.

        {{JsonSerializer.Serialize(packets, JsonSerializerOptions)}}
        """;

    private static string? CleanOneLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var clean = value.Trim().ReplaceLineEndings(" ");
        while (clean.Contains("  ", StringComparison.Ordinal))
        {
            clean = clean.Replace("  ", " ", StringComparison.Ordinal);
        }

        return clean.Length <= 240 ? clean : clean[..240].TrimEnd();
    }

    private sealed class RebalanceAdvisoryEnvelope
    {
        public List<RebalanceAdvisoryItem> Suggestions { get; set; } = [];
    }

    private sealed class RebalanceAdvisoryItem
    {
        public string TicketId { get; set; } = string.Empty;
        public string? Rationale { get; set; }
        public string? RiskSummary { get; set; }
        public string? TradeoffSummary { get; set; }
        public string? ConfidenceWording { get; set; }
    }

    private sealed class OpenAiChatRequest
    {
        public string Model { get; set; } = string.Empty;
        public List<OpenAiChatMessage> Messages { get; set; } = [];
        public decimal Temperature { get; set; }

        [JsonPropertyName("max_tokens")]
        public int MaxTokens { get; set; }

        [JsonPropertyName("response_format")]
        public OpenAiResponseFormat? ResponseFormat { get; set; }
    }

    private sealed class OpenAiChatMessage
    {
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
    }

    private sealed class OpenAiResponseFormat
    {
        public string Type { get; set; } = "json_object";
    }

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
}
