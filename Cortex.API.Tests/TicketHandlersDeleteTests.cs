using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Handlers;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.AspNetCore.Http;
using Moq;

namespace Cortex.API.Tests;

public class TicketHandlersDeleteTests
{
    [Fact]
    public async Task DeleteTicket_ReturnsNoContent_AndPublishesLocalizedDeleteEvent()
    {
        const string ticketId = "1001";
        var ticket = new Ticket
        {
            Id = ticketId,
            Title = "Test",
            Description = "Desc",
            Status = "New",
            Priority = "Medium",
            BoardId = 1,
            CreatedBy = 5,
            CreatedDate = DateTime.UtcNow,
            LastModifiedBy = 5,
            RowVersion = [1, 0, 0, 0, 0, 0, 0, 1],
        };

        var repo = new Mock<ITicketRepository>(MockBehavior.Strict);
        repo.Setup(r => r.GetTicketByIdAsync(ticketId)).ReturnsAsync(ticket);
        repo.Setup(r => r.DeleteTicketAsync(ticketId)).ReturnsAsync(true);
        repo.Setup(r => r.SaveChangesAsync()).Returns(Task.CompletedTask);

        var visibility = new Mock<ITicketVisibilityService>(MockBehavior.Strict);
        visibility
            .Setup(v => v.GetCurrentVisibilityAsync())
            .ReturnsAsync(new TicketVisibilityContext(1, "U", "u@test.com", TicketVisibilityScope.All));

        RealtimeEventMessage? published = null;
        var realtime = new Mock<IRealtimeEventService>(MockBehavior.Strict);
        realtime
            .Setup(r => r.PublishAsync(It.IsAny<RealtimeEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<RealtimeEventMessage, CancellationToken>((m, _) => published = m)
            .Returns(ValueTask.CompletedTask);

        var audience = new Mock<IRealtimeAudienceResolver>(MockBehavior.Strict);
        audience
            .Setup(a => a.GetAudienceUserIdsAsync(ticket, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { 1, 2, 5 });

        var result = await TicketHandlers.DeleteTicket(
            ticketId,
            repo.Object,
            visibility.Object,
            realtime.Object,
            audience.Object);

        await ResultAssertions.AssertStatusCodeAsync(result, StatusCodes.Status204NoContent);

        repo.Verify(r => r.GetTicketByIdAsync(ticketId), Times.Once);
        repo.Verify(r => r.DeleteTicketAsync(ticketId), Times.Once);
        repo.Verify(r => r.SaveChangesAsync(), Times.Once);
        realtime.Verify(
            r => r.PublishAsync(It.IsAny<RealtimeEventMessage>(), It.IsAny<CancellationToken>()),
            Times.Once);

        Assert.NotNull(published);
        Assert.Equal("ticket.deleted", published.EventType);
        Assert.Equal(ticketId, published.TicketId);
        Assert.Equal(ticketId, published.EntityId);
        Assert.NotNull(published.AudienceUserIds);
        Assert.Equal(3, published.AudienceUserIds!.Length);
    }
}
