using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Cortex.API.Configuration;
using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cortex.API.Services;

/// <summary>
/// Internal Cortex Memory v2 embedding foundation. Embeddings are generated and persisted for future
/// semantic retrieval only; CortexInsightService remains keyword-based until vector search is wired in.
/// </summary>
public sealed class CortexEmbeddingService : ICortexEmbeddingService
{
    private const string OpenAiEmbeddingsUrl = "https://api.openai.com/v1/embeddings";
    private const string InputSchemaVersion = "ticket-embedding-input:v1";

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly CortexDbContext _db;
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly ILogger<CortexEmbeddingService> _logger;

    public CortexEmbeddingService(
        CortexDbContext db,
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        ILogger<CortexEmbeddingService> logger)
    {
        _db = db;
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<TicketEmbedding?> EnsureEmbeddingAsync(
        string ticketId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticketId))
        {
            return null;
        }

        var normalizedTicketId = ticketId.Trim();
        var embeddingModel = _options.ResolvedEmbeddingModel;

        var ticket = await _db.Tickets
            .AsNoTracking()
            .SingleOrDefaultAsync(
                ticket => ticket.Id == normalizedTicketId,
                cancellationToken);
        if (ticket is null)
        {
            _logger.LogWarning(
                "Cortex Memory embedding skipped because ticket {TicketId} was not found.",
                normalizedTicketId);
            return null;
        }

        ticket.BoardDefinition = await _db.TicketBoardDefinitions
            .AsNoTracking()
            .SingleOrDefaultAsync(
                board => board.Id == ticket.BoardId,
                cancellationToken);

        var inputText = BuildEmbeddingInputText(ticket);
        var contentHash = ComputeContentHash(inputText);
        var existing = await _db.TicketEmbeddings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                embedding => embedding.TicketId == ticket.Id
                    && embedding.EmbeddingModel == embeddingModel,
                cancellationToken);

        if (existing is not null
            && string.Equals(existing.ContentHash, contentHash, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(existing.VectorJson)
            && !string.Equals(existing.VectorJson.Trim(), "[]", StringComparison.Ordinal))
        {
            return existing;
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            _logger.LogWarning(
                "Cortex Memory embedding skipped for ticket {TicketId} because OpenAI:ApiKey is not configured.",
                ticket.Id);
            return null;
        }

        var vector = await TryGenerateEmbeddingAsync(
            ticket.Id,
            embeddingModel,
            inputText,
            cancellationToken);
        if (vector is null)
        {
            return null;
        }

        return await UpsertEmbeddingAsync(
            ticket,
            embeddingModel,
            vector,
            cancellationToken);
    }

    public string BuildEmbeddingInputText(Ticket ticket)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var builder = new StringBuilder();
        builder.AppendLine(InputSchemaVersion);
        AppendField(builder, "Title", ticket.Title);
        AppendField(builder, "Description", ticket.Description);
        AppendField(builder, "Priority", ticket.Priority);
        AppendField(builder, "Board", ticket.BoardDefinition?.Name ?? ticket.BoardId.ToString());
        AppendField(builder, "StoryPoints", ticket.StoryPoints?.ToString());

        return builder.ToString().TrimEnd();
    }

    public string ComputeContentHash(Ticket ticket)
    {
        var inputText = BuildEmbeddingInputText(ticket);
        return ComputeContentHash(inputText);
    }

    public async Task<bool> NeedsRegenerationAsync(
        Ticket ticket,
        string embeddingModel,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var normalizedModel = NormalizeEmbeddingModel(embeddingModel);
        var contentHash = ComputeContentHash(ticket);

        var existing = await _db.TicketEmbeddings
            .AsNoTracking()
            .SingleOrDefaultAsync(
                embedding => embedding.TicketId == ticket.Id
                    && embedding.EmbeddingModel == normalizedModel,
                cancellationToken);

        return existing is null
            || !string.Equals(existing.ContentHash, contentHash, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(existing.VectorJson)
            || string.Equals(existing.VectorJson.Trim(), "[]", StringComparison.Ordinal);
    }

    public async Task<TicketEmbedding> UpsertEmbeddingAsync(
        Ticket ticket,
        string embeddingModel,
        IReadOnlyList<float> vector,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);
        ArgumentNullException.ThrowIfNull(vector);

        var normalizedModel = NormalizeEmbeddingModel(embeddingModel);
        var contentHash = ComputeContentHash(ticket);
        var now = DateTime.UtcNow;
        var vectorJson = JsonSerializer.Serialize(vector);

        var embedding = await _db.TicketEmbeddings
            .SingleOrDefaultAsync(
                stored => stored.TicketId == ticket.Id
                    && stored.EmbeddingModel == normalizedModel,
                cancellationToken);

        if (embedding is null)
        {
            embedding = new TicketEmbedding
            {
                TicketId = ticket.Id,
                EmbeddingModel = normalizedModel,
                CreatedAtUtc = now,
            };
            _db.TicketEmbeddings.Add(embedding);
        }

        embedding.ContentHash = contentHash;
        embedding.VectorJson = vectorJson;
        embedding.UpdatedAtUtc = now;

        await _db.SaveChangesAsync(cancellationToken);
        return embedding;
    }

    private async Task<IReadOnlyList<float>?> TryGenerateEmbeddingAsync(
        string ticketId,
        string embeddingModel,
        string inputText,
        CancellationToken cancellationToken)
    {
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, OpenAiEmbeddingsUrl);
            httpRequest.Headers.Accept.Add(
                new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            httpRequest.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey!.Trim());

            var requestBody = new OpenAiEmbeddingRequest
            {
                Model = embeddingModel,
                Input = inputText,
            };
            var json = JsonSerializer.Serialize(requestBody, JsonSerializerOptions);
            httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OpenAI embedding request failed for ticket {TicketId}. StatusCode={StatusCode} ReasonPhrase={ReasonPhrase} ResponseLength={ResponseLength}",
                    ticketId,
                    (int)response.StatusCode,
                    response.ReasonPhrase,
                    responseBody.Length);
                return null;
            }

            var parsed = JsonSerializer.Deserialize<OpenAiEmbeddingResponse>(
                responseBody,
                JsonSerializerOptions);
            var vector = parsed?.Data?.FirstOrDefault()?.Embedding;
            if (vector is null || vector.Count == 0)
            {
                _logger.LogWarning(
                    "OpenAI embedding response for ticket {TicketId} did not include an embedding vector.",
                    ticketId);
                return null;
            }

            return vector;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "OpenAI embedding request timed out or was canceled for ticket {TicketId}.",
                ticketId);
            return null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "OpenAI embedding request failed for ticket {TicketId}.",
                ticketId);
            return null;
        }
    }

    private static void AppendField(StringBuilder builder, string label, string? value)
    {
        var normalized = NormalizeMeaningfulText(value);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return;
        }

        builder.Append(label);
        builder.Append(": ");
        builder.AppendLine(normalized);
    }

    private static string NormalizeMeaningfulText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var character in value.Trim())
        {
            if (char.IsWhiteSpace(character))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }

            builder.Append(character);
        }

        return builder.ToString();
    }

    private static string ComputeContentHash(string inputText)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(inputText));
        return Convert.ToHexString(hashBytes);
    }

    private static string NormalizeEmbeddingModel(string embeddingModel)
    {
        if (string.IsNullOrWhiteSpace(embeddingModel))
        {
            throw new ArgumentException("Embedding model is required.", nameof(embeddingModel));
        }

        return embeddingModel.Trim();
    }

    private sealed class OpenAiEmbeddingRequest
    {
        public string Model { get; set; } = string.Empty;
        public string Input { get; set; } = string.Empty;
    }

    private sealed class OpenAiEmbeddingResponse
    {
        public List<OpenAiEmbeddingData>? Data { get; set; }
    }

    private sealed class OpenAiEmbeddingData
    {
        public List<float>? Embedding { get; set; }
    }
}
