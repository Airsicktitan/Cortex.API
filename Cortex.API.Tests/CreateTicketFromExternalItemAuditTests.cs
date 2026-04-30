using System.Text.Json;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Cortex.API.Services.Integrations;
using Cortex.API.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Tests;

public class CreateTicketFromExternalItemAuditTests
{
    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"ext-ticket-audit-{Guid.NewGuid():N}")
            .Options;
        return new CortexDbContext(options);
    }

    private static void SeedBoard(CortexDbContext ctx)
    {
        ctx.TicketBoardDefinitions.Add(
            new TicketBoardDefinition
            {
                Name = $"B-{Guid.NewGuid():N}",
                Description = "Test",
                RequiresStoryPoints = false,
                IsEnabled = true,
                CreatedDateUtc = DateTime.UtcNow,
            });
    }

    private static async Task<ExternalWorkSourceResponse?> CreateSourceAsync(
        IExternalIntegrationService service,
        string sourceName = "SAP Support Requests")
    {
        var connection = await service.CreateConnectionAsync(
            new CreateIntegrationConnectionRequest
            {
                Provider = IntegrationProvider.SharePoint,
                DisplayName = "Conn",
            });

        return await service.CreateSourceAsync(
            connection.Id,
            new CreateExternalWorkSourceRequest
            {
                Provider = IntegrationProvider.SharePoint,
                SourceType = ExternalSourceType.SharePointList,
                ExternalSourceId = $"list-{Guid.NewGuid():N}",
                Name = sourceName,
            });
    }

    [Fact]
    public async Task CreateTicketFromExternalItem_OnSuccess_WritesExternalItemPromotedAudit()
    {
        await using var ctx = CreateContext();
        SeedBoard(ctx);
        await ctx.SaveChangesAsync();
        var boardId = await ctx.TicketBoardDefinitions.Select(b => b.Id).FirstAsync();

        var service = IntegrationServiceTestFactory.Create(ctx);
        var source = await CreateSourceAsync(service);
        await service.ReplaceBoardMappingsAsync(
            source!.Id,
            [
                new ExternalBoardMappingItemRequest
                {
                    BoardId = boardId,
                    MappingMode = ExternalBoardMappingMode.ReferenceOnly,
                    IsDefault = true,
                },
            ]);

        var item = await service.ManualUpsertWorkItemAsync(
            source.Id,
            new ManualUpsertExternalWorkItemRequest
            {
                ExternalItemId = "SP-2001",
                Title = "From external",
                ExternalUrl = "https://example.invalid/item",
                RawJson = """{"secret":"nope"}""",
            });

        var fake = new CapturingFakeTicketCreationApplicationService();
        var createService = IntegrationServiceTestFactory.Create(ctx, ticketCreation: fake);

        var result = await createService.CreateTicketFromExternalItemAsync(
            item!.Id,
            new CreateTicketFromExternalItemRequest());

        Assert.NotNull(result);
        var ticketId = result!.CortexTicketId;

        var audits = await ctx.TicketAuditEntries
            .Include(a => a.FieldChanges)
            .Where(a => a.TicketId == ticketId && a.Action == "ExternalItemPromotedToTicket")
            .ToListAsync();

        var entry = Assert.Single(audits);
        Assert.Contains("SP-2001", entry.Summary, StringComparison.Ordinal);
        Assert.Contains("SAP Support Requests", entry.Summary, StringComparison.Ordinal);
        Assert.Contains("SharePoint", entry.Summary, StringComparison.Ordinal);
        Assert.Contains("Cortex ticket created from external work item", entry.Reason ?? "", StringComparison.Ordinal);
        Assert.Contains("External source was not updated", entry.Reason ?? "", StringComparison.Ordinal);

        Assert.Contains(
            entry.FieldChanges,
            c => c.FieldName == "External item ID" && c.NewValue == "SP-2001");
        Assert.Contains(
            entry.FieldChanges,
            c => c.FieldName == "Provider" && c.NewValue == "SharePoint");
        Assert.Contains(
            entry.FieldChanges,
            c => c.FieldName == "Source" && c.NewValue == "SAP Support Requests");
        Assert.Contains(
            entry.FieldChanges,
            c => c.FieldName == "Cortex ticket ID" && c.NewValue == ticketId);

        var fcJson = JsonSerializer.Serialize(
            entry.FieldChanges,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        Assert.DoesNotContain("rawJson", fcJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", fcJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nope", fcJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CreateTicketFromExternalItem_OnCreateFailure_DoesNotWritePromoteAudit()
    {
        await using var ctx = CreateContext();
        SeedBoard(ctx);
        await ctx.SaveChangesAsync();
        var boardId = await ctx.TicketBoardDefinitions.Select(b => b.Id).FirstAsync();

        var setup = IntegrationServiceTestFactory.Create(ctx);
        var source = await CreateSourceAsync(setup, "List");
        await setup.ReplaceBoardMappingsAsync(
            source!.Id,
            [
                new ExternalBoardMappingItemRequest
                {
                    BoardId = boardId,
                    MappingMode = ExternalBoardMappingMode.ReferenceOnly,
                    IsDefault = true,
                },
            ]);

        var item = await setup.ManualUpsertWorkItemAsync(
            source.Id,
            new ManualUpsertExternalWorkItemRequest
            {
                ExternalItemId = "fail-audit",
                Title = "T",
            });

        var fake = new CapturingFakeTicketCreationApplicationService
        {
            ThrowOnCreate = new InvalidOperationException("simulated"),
        };
        var service = IntegrationServiceTestFactory.Create(ctx, ticketCreation: fake);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateTicketFromExternalItemAsync(item!.Id, new CreateTicketFromExternalItemRequest()));

        var promoteCount = await ctx.TicketAuditEntries.CountAsync(
            a => a.Action == "ExternalItemPromotedToTicket");
        Assert.Equal(0, promoteCount);

        var row = await ctx.ExternalWorkItems.AsNoTracking().SingleAsync();
        Assert.Null(row.CortexTicketId);
    }

    [Fact]
    public async Task CreateTicketFromExternalItem_AlreadyLinked_DoesNotAddSecondPromoteAudit()
    {
        await using var ctx = CreateContext();
        SeedBoard(ctx);
        await ctx.SaveChangesAsync();
        var boardId = await ctx.TicketBoardDefinitions.Select(b => b.Id).FirstAsync();

        var setup = IntegrationServiceTestFactory.Create(ctx);
        var source = await CreateSourceAsync(setup, "Src");
        await setup.ReplaceBoardMappingsAsync(
            source!.Id,
            [
                new ExternalBoardMappingItemRequest
                {
                    BoardId = boardId,
                    MappingMode = ExternalBoardMappingMode.ReferenceOnly,
                    IsDefault = true,
                },
            ]);

        var item = await setup.ManualUpsertWorkItemAsync(
            source.Id,
            new ManualUpsertExternalWorkItemRequest
            {
                ExternalItemId = "dup-audit",
                Title = "T",
            });

        var fakeOk = new CapturingFakeTicketCreationApplicationService();
        var serviceOk = IntegrationServiceTestFactory.Create(ctx, ticketCreation: fakeOk);
        await serviceOk.CreateTicketFromExternalItemAsync(item!.Id, new CreateTicketFromExternalItemRequest());

        var countAfterFirst = await ctx.TicketAuditEntries.CountAsync(
            a => a.Action == "ExternalItemPromotedToTicket");
        Assert.Equal(1, countAfterFirst);

        var fakeSecond = new CapturingFakeTicketCreationApplicationService();
        var serviceSecond = IntegrationServiceTestFactory.Create(ctx, ticketCreation: fakeSecond);

        await Assert.ThrowsAsync<ExternalWorkItemAlreadyLinkedException>(() =>
            serviceSecond.CreateTicketFromExternalItemAsync(item.Id, new CreateTicketFromExternalItemRequest()));

        var countAfterSecond = await ctx.TicketAuditEntries.CountAsync(
            a => a.Action == "ExternalItemPromotedToTicket");
        Assert.Equal(1, countAfterSecond);
        Assert.Empty(fakeSecond.CapturedRequests);
    }
}
