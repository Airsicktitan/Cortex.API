using Cortex.API.Database;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Tests;

public class SapTicketReferenceDetectionServiceTests
{
    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"sap-ticket-detect-{Guid.NewGuid():N}")
            .Options;
        return new CortexDbContext(options);
    }

    [Fact]
    public async Task DisabledSource_DoesNotProduceMatches()
    {
        await using var ctx = CreateContext();
        var now = DateTime.UtcNow;
        var disabled = new SapReferenceSource
        {
            Name = "Off",
            SourceType = SapReferenceSourceType.Manual,
            IsEnabled = false,
            CreatedAtUtc = now,
        };
        var enabled = new SapReferenceSource
        {
            Name = "On",
            SourceType = SapReferenceSourceType.Manual,
            IsEnabled = true,
            CreatedAtUtc = now,
        };
        ctx.SapReferenceSources.AddRange(disabled, enabled);
        await ctx.SaveChangesAsync();

        var offTable = new SapTableMetadata
        {
            SapReferenceSourceId = disabled.Id,
            TableName = "MARC",
            Description = "Off",
            CreatedAtUtc = now,
        };
        var onTable = new SapTableMetadata
        {
            SapReferenceSourceId = enabled.Id,
            TableName = "MARA",
            Description = "On",
            CreatedAtUtc = now,
        };
        ctx.SapTables.AddRange(offTable, onTable);
        await ctx.SaveChangesAsync();

        var svc = new SapTicketReferenceDetectionService(ctx);
        var ticket = new Ticket
        {
            Id = "T-1",
            Title = "MARC issue",
            Description = "See MARA and MARC",
            Status = "New",
            ApprovalStatus = ApprovalStatus.Approved,
            Priority = "Medium",
            BoardId = 1,
            CreatedBy = 1,
            LastModifiedBy = 1,
        };

        var dto = await svc.DetectSapReferencesForTicketAsync(ticket);

        Assert.Contains(dto.Matches, m => m.TableName == "MARA");
        Assert.DoesNotContain(dto.Matches, m => m.TableName == "MARC");
    }
}
