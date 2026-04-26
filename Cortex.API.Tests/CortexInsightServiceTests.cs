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

    private static CortexDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new CortexDbContext(options);
    }

    private static CortexInsightService CreateService(
        CortexDbContext db,
        StubHttpMessageHandler handler)
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
            NullLogger<CortexInsightService>.Instance);
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
