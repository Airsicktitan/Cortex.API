using Cortex.API.Database;
using Cortex.API.Handlers;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Cortex.API.Tests;

public class CortexMemoryFeedbackServiceTests
{
    [Fact]
    public async Task RecordAsync_PersistsEventWithAllFields()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        await service.RecordAsync(
            ticketId: "T-1",
            eventType: CortexMemoryEventType.RelatedTicketShown,
            source: "CortexInsight",
            relatedTicketId: "T-2",
            createdByUserId: 5,
            createdByDisplayName: "Alice",
            metadataJson: "{\"confidenceScore\":80}");

        var saved = await db.CortexMemoryFeedbackEvents.SingleAsync();
        Assert.Equal("T-1", saved.TicketId);
        Assert.Equal("T-2", saved.RelatedTicketId);
        Assert.Equal(CortexMemoryEventType.RelatedTicketShown, saved.EventType);
        Assert.Equal("CortexInsight", saved.Source);
        Assert.Equal("{\"confidenceScore\":80}", saved.MetadataJson);
        Assert.Equal(5, saved.CreatedByUserId);
        Assert.Equal("Alice", saved.CreatedByDisplayName);
    }

    [Fact]
    public async Task RecordAsync_ToleratesMissingOptionalFields()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        await service.RecordAsync(
            ticketId: "T-1",
            eventType: CortexMemoryEventType.AiSuggestionAccepted,
            source: "TicketTriage");

        var saved = await db.CortexMemoryFeedbackEvents.SingleAsync();
        Assert.Equal("T-1", saved.TicketId);
        Assert.Null(saved.RelatedTicketId);
        Assert.Null(saved.MetadataJson);
        Assert.Null(saved.CreatedByUserId);
        Assert.Null(saved.CreatedByDisplayName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RecordAsync_SkipsRecord_WhenTicketIdIsBlank(string blank)
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        await service.RecordAsync(blank, CortexMemoryEventType.RelatedTicketShown, "CortexInsight");

        Assert.Empty(db.CortexMemoryFeedbackEvents);
    }

    [Fact]
    public async Task RecordAsync_SkipsRecord_WhenEventTypeIsBlank()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        await service.RecordAsync("T-1", "", "CortexInsight");

        Assert.Empty(db.CortexMemoryFeedbackEvents);
    }

    [Fact]
    public async Task RecordAsync_DoesNotThrow_WhenDbSaveFails()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        var db = new CortexDbContext(options);
        await db.DisposeAsync();

        var service = CreateService(db);

        var exception = await Record.ExceptionAsync(() =>
            service.RecordAsync("T-1", CortexMemoryEventType.RelatedTicketShown, "CortexInsight"));
        Assert.Null(exception);
    }

    [Theory]
    [InlineData(CortexMemoryEventType.RelatedTicketShown)]
    [InlineData(CortexMemoryEventType.RelatedTicketClicked)]
    [InlineData(CortexMemoryEventType.AiSuggestionAccepted)]
    [InlineData(CortexMemoryEventType.OwnerOverridden)]
    [InlineData(CortexMemoryEventType.PriorityOverridden)]
    [InlineData(CortexMemoryEventType.StatusOverridden)]
    public void IsValid_ReturnsTrue_ForAllSupportedEventTypes(string eventType)
    {
        Assert.True(CortexMemoryEventType.IsValid(eventType));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("unknown")]
    [InlineData("relatedticketshown")]
    public void IsValid_ReturnsFalse_ForUnknownOrBlankValues(string? eventType)
    {
        Assert.False(CortexMemoryEventType.IsValid(eventType));
    }

    [Fact]
    public async Task PostMemoryFeedback_ReturnsBadRequest_ForInvalidEventType()
    {
        await using var db = CreateDbContext();
        var captured = new List<string>();
        var feedbackService = new CapturingFeedbackService(captured);
        var userContext = CreateUserContext(userId: 1, displayName: "Tester");

        var result = await TicketHandlers.PostMemoryFeedback(
            ticketId: "T-1",
            request: new Cortex.API.DTO.CortexMemoryFeedbackRequest
            {
                EventType = "not-a-real-event",
                Source = "Frontend",
            },
            userContext: userContext,
            feedbackService: feedbackService);

        Assert.IsAssignableFrom<Microsoft.AspNetCore.Http.IResult>(result);
        var httpResult = Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.BadRequest<object>>(result);
        Assert.Empty(captured);
    }

    [Fact]
    public async Task PostMemoryFeedback_ReturnsNoContent_ForValidEventType()
    {
        await using var db = CreateDbContext();
        var captured = new List<string>();
        var feedbackService = new CapturingFeedbackService(captured);
        var userContext = CreateUserContext(userId: 1, displayName: "Tester");

        var result = await TicketHandlers.PostMemoryFeedback(
            ticketId: "T-1",
            request: new Cortex.API.DTO.CortexMemoryFeedbackRequest
            {
                EventType = CortexMemoryEventType.RelatedTicketClicked,
                Source = "Frontend",
                RelatedTicketId = "T-2",
            },
            userContext: userContext,
            feedbackService: feedbackService);

        Assert.IsType<Microsoft.AspNetCore.Http.HttpResults.NoContent>(result);
        Assert.Single(captured);
        Assert.Equal(CortexMemoryEventType.RelatedTicketClicked, captured[0]);
    }

    private static CortexDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new CortexDbContext(options);
    }

    private static CortexMemoryFeedbackService CreateService(CortexDbContext db) =>
        new(db, NullLogger<CortexMemoryFeedbackService>.Instance);

    private static IUserContextService CreateUserContext(int userId, string displayName)
    {
        var mock = new Moq.Mock<IUserContextService>(Moq.MockBehavior.Strict);
        mock.Setup(s => s.GetCurrentUserAsync())
            .ReturnsAsync(new Cortex.API.Models.User { Id = userId, DisplayName = displayName, Email = "test@test.com" });
        return mock.Object;
    }

    private sealed class CapturingFeedbackService : ICortexMemoryFeedbackService
    {
        private readonly List<string> _eventTypes;
        public CapturingFeedbackService(List<string> eventTypes) => _eventTypes = eventTypes;

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
            _eventTypes.Add(eventType);
            return Task.CompletedTask;
        }
    }
}
