using System.Net;
using System.Text;
using System.Text.Json;
using Cortex.API.Configuration;
using Cortex.API.Database;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Cortex.API.Tests;

public class CortexInsightServiceTests
{
    [Fact]
    public async Task GetInsightAsync_ReturnsEmpty_WhenNoSimilarTicketsExist()
    {
        await using var db = CreateDbContext();
        var current = NewTicket(
            id: "T-100",
            title: "Payroll approval setup",
            description: "Need approval routing for payroll exports.");
        db.Tickets.Add(current);
        db.Tickets.Add(NewTicket(
            id: "T-101",
            title: "Warehouse label printer offline",
            description: "Printer queue needs attention."));
        await db.SaveChangesAsync();

        var handler = new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("AI should not be called when no similar tickets exist."));
        var service = CreateService(db, handler);

        var result = await service.GetInsightAsync(
            current,
            AllVisible(),
            CancellationToken.None);

        Assert.Equal("T-100", result.TicketId);
        Assert.Empty(result.Matches);
        Assert.False(result.Unavailable);
        Assert.Null(result.Summary);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task GetInsightAsync_ReturnsInsight_WhenMatchesExist()
    {
        await using var db = CreateDbContext();
        var current = NewTicket(
            id: "T-200",
            title: "Nightly export timeout for customer feed",
            description: "The nightly customer export fails with a timeout.");
        var match = NewTicket(
            id: "T-201",
            title: "Customer feed export timeout",
            description: "Nightly export failed before the file was generated.",
            status: "Resolved",
            createdDate: new DateTime(2026, 4, 20, 12, 0, 0, DateTimeKind.Utc));
        match.Comments.Add(new Comment
        {
            TicketId = match.Id,
            Body = "Restarted the scheduler and cleared the stuck export batch.",
            CreatedBy = 1,
            CreatedDate = new DateTime(2026, 4, 20, 13, 0, 0, DateTimeKind.Utc),
            LastModifiedDate = new DateTime(2026, 4, 20, 13, 0, 0, DateTimeKind.Utc),
        });

        db.Tickets.AddRange(current, match);
        await db.SaveChangesAsync();

        var handler = new StubHttpMessageHandler((_, _) =>
            OpenAiResponse(new
            {
                summary = "A prior customer feed export timed out during the nightly run.",
                resolution = "The prior ticket was resolved by restarting the scheduler and clearing the stuck batch.",
                rootCause = "A stuck export batch blocked scheduler progress.",
                suggestedNextStep = "Check the scheduler state and clear any stuck export batch before rerunning the feed.",
            }));
        var service = CreateService(db, handler);

        var result = await service.GetInsightAsync(
            current,
            AllVisible(),
            CancellationToken.None);

        Assert.False(result.Unavailable);
        Assert.Single(result.Matches);
        Assert.Equal("T-201", result.Matches[0].Id);
        Assert.Equal("T-201", result.Matches[0].SourceTicketId);
        Assert.Equal("/tickets/T-201", result.Matches[0].SourceUrl);
        Assert.Equal("Resolved", result.Matches[0].Status);
        Assert.Equal(
            "Restarted the scheduler and cleared the stuck export batch.",
            result.Matches[0].SourceQuote);
        Assert.InRange(result.ConfidenceScore, 1, 100);
        Assert.Contains(result.MatchReasons, reason => reason.Contains("Title terms match", StringComparison.Ordinal));
        Assert.Contains(result.Matches[0].MatchReasons, reason => reason.Contains("Prior ticket is Resolved", StringComparison.Ordinal));
        Assert.Equal("A prior customer feed export timed out during the nightly run.", result.Summary);
        Assert.Equal(
            "The prior ticket was resolved by restarting the scheduler and clearing the stuck batch.",
            result.Resolution);
        Assert.Equal("A stuck export batch blocked scheduler progress.", result.RootCause);
        Assert.Equal(
            "Check the scheduler state and clear any stuck export batch before rerunning the feed.",
            result.SuggestedNextStep);
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task GetInsightAsync_DoesNotThrow_WhenSimilarTicketHasNoComments()
    {
        await using var db = CreateDbContext();
        var current = NewTicket(
            id: "T-300",
            title: "Invoice export validation timeout",
            description: "Invoice export validation fails with a timeout.");
        var match = NewTicket(
            id: "T-301",
            title: "Invoice export timeout",
            description: "Validation timed out during invoice export.",
            status: "Closed");

        db.Tickets.AddRange(current, match);
        await db.SaveChangesAsync();

        var handler = new StubHttpMessageHandler((_, _) =>
            OpenAiResponse(new
            {
                summary = "Prior invoice export tickets timed out during validation.",
                resolution = "The prior ticket was closed without a meaningful comment.",
                rootCause = "Not enough evidence.",
                suggestedNextStep = "Review validation logs for the current invoice export run.",
            }));
        var service = CreateService(db, handler);

        var result = await service.GetInsightAsync(
            current,
            AllVisible(),
            CancellationToken.None);

        Assert.False(result.Unavailable);
        Assert.Single(result.Matches);
        Assert.Null(result.Matches[0].LastMeaningfulComment);
        Assert.Equal("Not enough evidence.", result.RootCause);
    }

    [Fact]
    public async Task GetInsightAsync_UsesCache_AndInvalidatesWhenCommentsChange()
    {
        await using var db = CreateDbContext();
        var current = NewTicket(
            id: "T-400",
            title: "Payment export timeout",
            description: "Payment export timeout during nightly run.");
        var match = NewTicket(
            id: "T-401",
            title: "Payment export timeout",
            description: "Payment export timed out before completion.",
            status: "Resolved");
        match.Comments.Add(new Comment
        {
            TicketId = match.Id,
            Body = "Cleared the stuck payment export job.",
            CreatedBy = 1,
            CreatedDate = new DateTime(2026, 4, 21, 13, 0, 0, DateTimeKind.Utc),
            LastModifiedDate = new DateTime(2026, 4, 21, 13, 0, 0, DateTimeKind.Utc),
        });

        db.Tickets.AddRange(current, match);
        await db.SaveChangesAsync();

        var call = 0;
        var handler = new StubHttpMessageHandler((_, _) =>
        {
            call++;
            return OpenAiResponse(new
            {
                summary = $"summary-{call}",
                resolution = "Resolved from source ticket.",
                rootCause = "Stuck export job.",
                suggestedNextStep = "Check the export job state.",
            });
        });
        var service = CreateService(db, handler);

        var first = await service.GetInsightAsync(current, AllVisible(), CancellationToken.None);
        var cached = await service.GetInsightAsync(current, AllVisible(), CancellationToken.None);

        Assert.Equal("summary-1", first.Summary);
        Assert.Equal("summary-1", cached.Summary);
        Assert.Equal(1, handler.CallCount);

        db.Comments.Add(new Comment
        {
            TicketId = match.Id,
            Body = "Confirmed payment export finished after clearing the retry lock.",
            CreatedBy = 1,
            CreatedDate = new DateTime(2026, 4, 22, 13, 0, 0, DateTimeKind.Utc),
            LastModifiedDate = new DateTime(2026, 4, 22, 13, 0, 0, DateTimeKind.Utc),
        });
        await db.SaveChangesAsync();

        var afterCommentChange = await service.GetInsightAsync(current, AllVisible(), CancellationToken.None);

        Assert.Equal("summary-2", afterCommentChange.Summary);
        Assert.Equal(2, handler.CallCount);
        Assert.Equal(
            "Confirmed payment export finished after clearing the retry lock.",
            afterCommentChange.Matches[0].SourceQuote);
    }

    [Fact]
    public async Task GetInsightAsync_SemanticMatch_RanksAboveWeakKeywordMatch()
    {
        await using var db = CreateDbContext();

        // Current ticket about network firewall drift
        var current = NewTicket(
            id: "T-SEM-1",
            title: "network firewall configuration drift",
            description: "firewall rules drifted after the patch window.");

        // Ticket A: different domain but identical embedding vector (cosine = 1.0)
        var semanticMatch = NewTicket(
            id: "T-SEM-2",
            title: "infrastructure audit completed",
            description: "quarterly infrastructure audit passed.",
            status: "Resolved");

        // Ticket B: shares "configuration" keyword, no embedding — falls back to keyword score
        var keywordMatch = NewTicket(
            id: "T-SEM-3",
            title: "database configuration drift",
            description: "config drifted on database cluster.");

        db.Tickets.AddRange(current, semanticMatch, keywordMatch);
        db.TicketEmbeddings.AddRange(
            new TicketEmbedding { TicketId = "T-SEM-1", EmbeddingModel = "m", ContentHash = "h1", VectorJson = "[1.0, 0.0, 0.0]" },
            new TicketEmbedding { TicketId = "T-SEM-2", EmbeddingModel = "m", ContentHash = "h2", VectorJson = "[1.0, 0.0, 0.0]" });
        await db.SaveChangesAsync();

        var service = CreateServiceWithAiDisabled(db);
        var result = await service.GetInsightAsync(current, AllVisible(), CancellationToken.None);

        Assert.True(result.Matches.Count >= 2, "Expected both candidates to score above threshold.");
        Assert.Equal("T-SEM-2", result.Matches[0].Id);
    }

    [Fact]
    public async Task GetInsightAsync_MissingEmbeddings_FallsBackToKeywordBehavior()
    {
        await using var db = CreateDbContext();
        var current = NewTicket(
            id: "T-KB-1",
            title: "invoice export timeout",
            description: "Invoice export validation fails with a timeout.");
        var match = NewTicket(
            id: "T-KB-2",
            title: "invoice export validation timeout",
            description: "Timeout during invoice validation step.",
            status: "Resolved");

        db.Tickets.AddRange(current, match);
        // No embeddings added — pure keyword fallback
        await db.SaveChangesAsync();

        var service = CreateServiceWithAiDisabled(db);
        var result = await service.GetInsightAsync(current, AllVisible(), CancellationToken.None);

        Assert.Single(result.Matches);
        Assert.Equal("T-KB-2", result.Matches[0].Id);
        // Keyword reasons should be present; no semantic reason
        Assert.DoesNotContain(
            result.Matches[0].MatchReasons,
            r => r.Contains("Semantically", StringComparison.Ordinal)
              || r.Contains("historical pattern", StringComparison.Ordinal));
    }

    [Fact]
    public async Task GetInsightAsync_LowSimilaritySemanticOnly_IsExcludedFromCandidates()
    {
        await using var db = CreateDbContext();

        // Current ticket has no keyword overlap with the candidate, and low cosine similarity.
        // cosine([1,0,0], [0.3, 0.954, 0]) ≈ 0.3 — below SemanticMediumConfidenceThreshold (0.50)
        var current = NewTicket(
            id: "T-LS-1",
            title: "authentication token expired",
            description: "user session token expired after idle timeout.");
        var lowSim = NewTicket(
            id: "T-LS-2",
            title: "quarterly infrastructure review",
            description: "infrastructure review notes from last quarter.");

        db.Tickets.AddRange(current, lowSim);
        db.TicketEmbeddings.AddRange(
            new TicketEmbedding { TicketId = "T-LS-1", EmbeddingModel = "m", ContentHash = "h1", VectorJson = "[1.0, 0.0, 0.0]" },
            new TicketEmbedding { TicketId = "T-LS-2", EmbeddingModel = "m", ContentHash = "h2", VectorJson = "[0.3, 0.954, 0.0]" });
        await db.SaveChangesAsync();

        var service = CreateServiceWithAiDisabled(db);
        var result = await service.GetInsightAsync(current, AllVisible(), CancellationToken.None);

        Assert.Empty(result.Matches);
    }

    [Fact]
    public async Task GetInsightAsync_MalformedVector_DoesNotThrow()
    {
        await using var db = CreateDbContext();
        var current = NewTicket(
            id: "T-MV-1",
            title: "payroll export timeout",
            description: "payroll batch fails during export.");
        var other = NewTicket(
            id: "T-MV-2",
            title: "payroll export timeout",
            description: "payroll batch timed out.",
            status: "Resolved");

        db.Tickets.AddRange(current, other);
        db.TicketEmbeddings.AddRange(
            new TicketEmbedding { TicketId = "T-MV-1", EmbeddingModel = "m", ContentHash = "h1", VectorJson = "not-valid-json" },
            new TicketEmbedding { TicketId = "T-MV-2", EmbeddingModel = "m", ContentHash = "h2", VectorJson = "[invalid,data]" });
        await db.SaveChangesAsync();

        var service = CreateServiceWithAiDisabled(db);

        // Should not throw; malformed vectors fall back to keyword scoring
        var result = await service.GetInsightAsync(current, AllVisible(), CancellationToken.None);

        Assert.Single(result.Matches);
        Assert.Equal("T-MV-2", result.Matches[0].Id);
    }

    [Fact]
    public async Task GetInsightAsync_CurrentTicketIsExcluded_FromSemanticCandidates()
    {
        await using var db = CreateDbContext();
        var current = NewTicket(
            id: "T-EX-1",
            title: "network timeout on vpn",
            description: "vpn connection drops after timeout.");
        var other = NewTicket(
            id: "T-EX-2",
            title: "network timeout on vpn",
            description: "vpn timeout recurring issue.",
            status: "Resolved");

        db.Tickets.AddRange(current, other);
        db.TicketEmbeddings.AddRange(
            new TicketEmbedding { TicketId = "T-EX-1", EmbeddingModel = "m", ContentHash = "h1", VectorJson = "[1.0, 0.0]" },
            new TicketEmbedding { TicketId = "T-EX-2", EmbeddingModel = "m", ContentHash = "h2", VectorJson = "[1.0, 0.0]" });
        await db.SaveChangesAsync();

        var service = CreateServiceWithAiDisabled(db);
        var result = await service.GetInsightAsync(current, AllVisible(), CancellationToken.None);

        Assert.DoesNotContain(result.Matches, m => m.Id == "T-EX-1");
    }

    [Fact]
    public async Task GetInsightAsync_SemanticReason_AppearsWhenSemanticScoreContributes()
    {
        await using var db = CreateDbContext();
        var current = NewTicket(
            id: "T-SR-1",
            title: "storage quota exceeded",
            description: "user storage quota exceeded on file server.");
        var highSim = NewTicket(
            id: "T-SR-2",
            title: "quota exceeded notification",
            description: "storage quota alert triggered.",
            status: "Resolved");
        var medSim = NewTicket(
            id: "T-SR-3",
            title: "disk space low warning",
            description: "disk space low on backup server.",
            status: "Resolved");

        db.Tickets.AddRange(current, highSim, medSim);
        db.TicketEmbeddings.AddRange(
            new TicketEmbedding { TicketId = "T-SR-1", EmbeddingModel = "m", ContentHash = "h1", VectorJson = "[1.0, 0.0, 0.0]" },
            // cosine = 1.0 → high confidence (≥ 0.75)
            new TicketEmbedding { TicketId = "T-SR-2", EmbeddingModel = "m", ContentHash = "h2", VectorJson = "[1.0, 0.0, 0.0]" },
            // cosine ≈ 0.6 → medium confidence (0.50-0.75): [0.6, 0.8] dot [1,0] / (1 * 1) = 0.6
            new TicketEmbedding { TicketId = "T-SR-3", EmbeddingModel = "m", ContentHash = "h3", VectorJson = "[0.6, 0.8, 0.0]" });
        await db.SaveChangesAsync();

        var service = CreateServiceWithAiDisabled(db);
        var result = await service.GetInsightAsync(current, AllVisible(), CancellationToken.None);

        var highMatch = result.Matches.FirstOrDefault(m => m.Id == "T-SR-2");
        var medMatch = result.Matches.FirstOrDefault(m => m.Id == "T-SR-3");

        Assert.NotNull(highMatch);
        Assert.Contains(
            highMatch.MatchReasons,
            r => r.Contains("Semantically similar to this ticket's request", StringComparison.Ordinal));

        Assert.NotNull(medMatch);
        Assert.Contains(
            medMatch.MatchReasons,
            r => r.Contains("Shares historical pattern with prior ticket", StringComparison.Ordinal));
    }

    private static CortexDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new CortexDbContext(options);
    }

    [Fact]
    public async Task GetInsightAsync_RecordsFeedback_ForEachMatch()
    {
        await using var db = CreateDbContext();
        var current = NewTicket(
            id: "T-FB-1",
            title: "invoice export timeout",
            description: "Invoice export validation fails with a timeout.");
        var match = NewTicket(
            id: "T-FB-2",
            title: "invoice export timeout",
            description: "Invoice export validation failed.",
            status: "Resolved");
        db.Tickets.AddRange(current, match);
        await db.SaveChangesAsync();

        var captured = new CapturingFeedbackService();
        var service = CreateServiceWithAiDisabled(db, feedbackService: captured);

        await service.GetInsightAsync(current, AllVisible(), CancellationToken.None);

        Assert.Single(captured.Calls);
        Assert.Equal("T-FB-1", captured.Calls[0].TicketId);
        Assert.Equal(CortexMemoryEventType.RelatedTicketShown, captured.Calls[0].EventType);
        Assert.Equal("T-FB-2", captured.Calls[0].RelatedTicketId);
    }

    [Fact]
    public async Task GetInsightAsync_DoesNotRecordFeedback_WhenNoMatches()
    {
        await using var db = CreateDbContext();
        var current = NewTicket(
            id: "T-FB-3",
            title: "xzqyplm gibberish",
            description: "xzqyplm gibberish.");
        db.Tickets.Add(current);
        await db.SaveChangesAsync();

        var captured = new CapturingFeedbackService();
        var service = CreateServiceWithAiDisabled(db, feedbackService: captured);

        await service.GetInsightAsync(current, AllVisible(), CancellationToken.None);

        Assert.Empty(captured.Calls);
    }

    private static CortexInsightService CreateService(
        CortexDbContext db,
        StubHttpMessageHandler handler,
        ICortexMemoryFeedbackService? feedbackService = null)
    {
        var aiSettingsService = new Mock<IAiSettingsService>(MockBehavior.Strict);
        aiSettingsService
            .Setup(service => service.GetAsync())
            .ReturnsAsync(new AiSettingsConfiguration
            {
                IsTriageEnabled = true,
                DefaultTextModel = "gpt-test",
                Temperature = 0.1,
                MaxTokens = 850,
                TimeoutSeconds = 30,
                RetryCount = 0,
            });

        return new CortexInsightService(
            db,
            new HttpClient(handler),
            Options.Create(new OpenAiOptions { ApiKey = "test-key" }),
            aiSettingsService.Object,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<CortexInsightService>.Instance,
            feedbackService ?? Mock.Of<ICortexMemoryFeedbackService>());
    }

    private static CortexInsightService CreateServiceWithAiDisabled(
        CortexDbContext db,
        ICortexMemoryFeedbackService? feedbackService = null)
    {
        var aiSettingsService = new Mock<IAiSettingsService>(MockBehavior.Strict);
        aiSettingsService
            .Setup(service => service.GetAsync())
            .ReturnsAsync(new AiSettingsConfiguration { IsTriageEnabled = false });

        return new CortexInsightService(
            db,
            new HttpClient(new StubHttpMessageHandler((_, _) =>
                throw new InvalidOperationException("AI should not be called when IsTriageEnabled = false."))),
            Options.Create(new OpenAiOptions { ApiKey = "test-key" }),
            aiSettingsService.Object,
            new MemoryCache(new MemoryCacheOptions()),
            NullLogger<CortexInsightService>.Instance,
            feedbackService ?? Mock.Of<ICortexMemoryFeedbackService>());
    }

    private static HttpResponseMessage OpenAiResponse(object content)
    {
        var contentJson = JsonSerializer.Serialize(content);
        var outerJson = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = contentJson,
                    },
                },
            },
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(outerJson, Encoding.UTF8, "application/json"),
        };
    }

    private static TicketVisibilityContext AllVisible() =>
        new(
            UserId: 1,
            DisplayName: "Tester",
            Email: "tester@example.com",
            Scope: TicketVisibilityScope.All);

    private static Ticket NewTicket(
        string id,
        string title,
        string description,
        string status = "New",
        DateTime? createdDate = null) =>
        new()
        {
            Id = id,
            Title = title,
            Description = description,
            Status = status,
            Priority = "Medium",
            BoardId = 1,
            CreatedBy = 1,
            LastModifiedBy = 1,
            CreatedDate = createdDate ?? new DateTime(2026, 4, 21, 12, 0, 0, DateTimeKind.Utc),
            LastModifiedDate = createdDate?.AddHours(2),
        };

    private sealed class CapturingFeedbackService : ICortexMemoryFeedbackService
    {
        public List<(string TicketId, string EventType, string? RelatedTicketId)> Calls { get; } = [];

        public Task RecordAsync(
            string ticketId,
            string eventType,
            string source,
            string? relatedTicketId = null,
            int? createdByUserId = null,
            string? createdByDisplayName = null,
            string? metadataJson = null,
            CancellationToken cancellationToken = default)
        {
            Calls.Add((ticketId, eventType, relatedTicketId));
            return Task.CompletedTask;
        }
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> _send;

        public StubHttpMessageHandler(
            Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> send)
        {
            _send = send;
        }

        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_send(request, cancellationToken));
        }
    }
}
