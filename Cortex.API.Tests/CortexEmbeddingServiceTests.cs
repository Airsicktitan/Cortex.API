using System.Net;
using System.Text;
using System.Text.Json;
using Cortex.API.Configuration;
using Cortex.API.Database;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cortex.API.Tests;

public class CortexEmbeddingServiceTests
{
    [Fact]
    public void ComputeContentHash_IgnoresWorkflowMetadata_AndNormalizesWhitespace()
    {
        using var db = CreateDbContext();
        var service = CreateService(db, new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("OpenAI should not be called.")));
        var first = NewTicket(
            title: "  Nightly   export\r\n timeout  ",
            description: "Customer feed   fails\nbefore generation.",
            status: "New",
            lastModifiedDate: new DateTime(2026, 4, 21, 12, 0, 0, DateTimeKind.Utc));
        var second = NewTicket(
            title: "Nightly export timeout",
            description: "Customer feed fails before generation.",
            status: "Resolved",
            lastModifiedDate: new DateTime(2026, 4, 22, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(service.ComputeContentHash(first), service.ComputeContentHash(second));
    }

    [Fact]
    public async Task EnsureEmbeddingAsync_SkipsOpenAiCall_WhenStoredHashMatches()
    {
        await using var db = CreateDbContext();
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("OpenAI should not be called when the embedding is current."));
        var service = CreateService(db, handler);
        var ticket = NewTicket(
            title: "Invoice export timeout",
            description: "Invoice export validation fails with a timeout.");
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        db.TicketEmbeddings.Add(new TicketEmbedding
        {
            TicketId = ticket.Id,
            EmbeddingModel = "text-embedding-test",
            ContentHash = service.ComputeContentHash(ticket),
            VectorJson = "[0.1,0.2]",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await service.EnsureEmbeddingAsync(ticket.Id);

        Assert.NotNull(result);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task EnsureEmbeddingAsync_CallsOpenAi_WhenMeaningfulContentChanged()
    {
        await using var db = CreateDbContext();
        var handler = new StubHttpMessageHandler((_, _) =>
            OpenAiEmbeddingResponse([0.11f, -0.22f, 0.33f]));
        var service = CreateService(
            db,
            handler,
            new OpenAiOptions { ApiKey = "test-key" });
        var ticket = NewTicket(
            title: "Invoice export timeout",
            description: "Invoice export validation fails with a timeout.");
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        await service.UpsertEmbeddingAsync(
            ticket,
            OpenAiOptions.DefaultEmbeddingModel,
            [0.1f, 0.2f]);
        ticket.Description = "Invoice export validation fails with a permissions error.";
        await db.SaveChangesAsync();

        var result = await service.EnsureEmbeddingAsync(ticket.Id);

        Assert.NotNull(result);
        Assert.Equal(1, handler.CallCount);
        Assert.Contains("\"model\":\"text-embedding-3-small\"", handler.LastRequestBody);

        var stored = await db.TicketEmbeddings
            .AsNoTracking()
            .SingleAsync(embedding => embedding.TicketId == ticket.Id);
        Assert.Equal(OpenAiOptions.DefaultEmbeddingModel, stored.EmbeddingModel);
        Assert.Equal(service.ComputeContentHash(ticket), stored.ContentHash);
        Assert.Equal([0.11f, -0.22f, 0.33f], JsonSerializer.Deserialize<List<float>>(stored.VectorJson));
    }

    [Fact]
    public async Task EnsureEmbeddingAsync_DoesNotThrow_WhenOpenAiConfigIsMissing()
    {
        await using var db = CreateDbContext();
        var handler = new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("OpenAI should not be called when the API key is missing."));
        var service = CreateService(
            db,
            handler,
            new OpenAiOptions { ApiKey = "" });
        var ticket = NewTicket(
            title: "Invoice export timeout",
            description: "Invoice export validation fails with a timeout.");
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        var result = await service.EnsureEmbeddingAsync(ticket.Id);

        Assert.Null(result);
        Assert.Equal(0, handler.CallCount);
        Assert.Empty(db.TicketEmbeddings);
    }

    [Fact]
    public async Task EnsureEmbeddingAsync_DoesNotThrow_WhenOpenAiFails()
    {
        await using var db = CreateDbContext();
        var handler = new StubHttpMessageHandler((_, _) => new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent(
                "{\"error\":{\"message\":\"temporary failure\"}}",
                Encoding.UTF8,
                "application/json"),
        });
        var service = CreateService(db, handler);
        var ticket = NewTicket(
            title: "Invoice export timeout",
            description: "Invoice export validation fails with a timeout.");
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        var result = await service.EnsureEmbeddingAsync(ticket.Id);

        Assert.Null(result);
        Assert.Equal(1, handler.CallCount);
        Assert.Empty(db.TicketEmbeddings);
    }

    [Fact]
    public async Task UpsertEmbeddingAsync_StoresModelHashAndVector()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db, new StubHttpMessageHandler((_, _) =>
            throw new InvalidOperationException("OpenAI should not be called by direct upsert.")));
        var ticket = NewTicket(
            title: "Invoice export timeout",
            description: "Invoice export validation fails with a timeout.");
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        var result = await service.UpsertEmbeddingAsync(
            ticket,
            " text-embedding-custom ",
            [0.1f, -0.2f]);

        Assert.Equal("text-embedding-custom", result.EmbeddingModel);
        Assert.Equal(service.ComputeContentHash(ticket), result.ContentHash);
        Assert.Equal([0.1f, -0.2f], JsonSerializer.Deserialize<List<float>>(result.VectorJson));
    }

    private static CortexDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new CortexDbContext(options);
    }

    private static CortexEmbeddingService CreateService(
        CortexDbContext db,
        StubHttpMessageHandler handler,
        OpenAiOptions? options = null) =>
        new(
            db,
            new HttpClient(handler),
            Options.Create(options ?? new OpenAiOptions
            {
                ApiKey = "test-key",
                EmbeddingModel = "text-embedding-test",
            }),
            NullLogger<CortexEmbeddingService>.Instance);

    private static HttpResponseMessage OpenAiEmbeddingResponse(IReadOnlyList<float> vector)
    {
        var json = JsonSerializer.Serialize(new
        {
            data = new[]
            {
                new { embedding = vector },
            },
        });

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private static Ticket NewTicket(
        string title,
        string description,
        string status = "New",
        DateTime? lastModifiedDate = null) =>
        new()
        {
            Id = Guid.NewGuid().ToString("N"),
            Title = title,
            Description = description,
            Status = status,
            Priority = "Medium",
            BoardId = 1,
            CreatedBy = 1,
            LastModifiedBy = 1,
            CreatedDate = new DateTime(2026, 4, 21, 12, 0, 0, DateTimeKind.Utc),
            LastModifiedDate = lastModifiedDate,
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
        public string? LastRequestBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastRequestBody = request.Content is null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);
            return _send(request, cancellationToken);
        }
    }
}
