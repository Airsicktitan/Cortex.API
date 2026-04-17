using Cortex.API.Data.Repositories;
using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Tests;

public class TicketRepositoryDeleteTests
{
    [Fact]
    public async Task DeleteTicketAsync_RemovesTicketFromDatabase()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"tickets-delete-{Guid.NewGuid():N}")
            .Options;

        await using (var arrange = new CortexDbContext(options))
        {
            arrange.Users.Add(
                new User
                {
                    Id = 1,
                    Email = "creator@test.invalid",
                    DisplayName = "Creator",
                    Role = Auth0Roles.User,
                });
            arrange.TicketBoardDefinitions.Add(
                new TicketBoardDefinition
                {
                    Id = 1,
                    Name = "Ticket",
                    RequiresStoryPoints = false,
                    IsEnabled = true,
                    CreatedDateUtc = DateTime.UtcNow,
                });
            arrange.Tickets.Add(
                new Ticket
                {
                    Id = "del-1",
                    Title = "X",
                    Description = "Y",
                    Status = "New",
                    Priority = "Medium",
                    BoardId = 1,
                    CreatedBy = 1,
                    CreatedDate = DateTime.UtcNow,
                    LastModifiedBy = 1,
                    RowVersion = [0, 0, 0, 0, 0, 0, 0, 1],
                });
            await arrange.SaveChangesAsync();
        }

        await using (var act = new CortexDbContext(options))
        {
            var repo = new TicketRepository(act);
            var deleted = await repo.DeleteTicketAsync("del-1");
            Assert.True(deleted);
            await repo.SaveChangesAsync();
        }

        await using (var assert = new CortexDbContext(options))
        {
            Assert.Null(await assert.Tickets.FirstOrDefaultAsync(t => t.Id == "del-1"));
        }
    }
}
