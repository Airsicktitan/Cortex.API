using Cortex.API.Models;

namespace Cortex.API.Tests;

public class TicketModelTests
{
    [Fact]
    public void CreatedDate_DefaultsToUtc()
    {
        var before = DateTime.UtcNow.AddSeconds(-1);

        var ticket = new Ticket();

        var after = DateTime.UtcNow.AddSeconds(1);
        Assert.Equal(DateTimeKind.Utc, ticket.CreatedDate.Kind);
        Assert.InRange(ticket.CreatedDate, before, after);
    }
}
