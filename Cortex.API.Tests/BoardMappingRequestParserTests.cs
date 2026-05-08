using System.Text.Json;
using System.Text.Json.Serialization;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Handlers;
using Cortex.API.Models;
using Cortex.API.Services;
using Cortex.API.Tests.TestDoubles;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Tests;

public class BoardMappingRequestParserTests
{
    private static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task ParseRequestBody_QaRawArray_Parses()
    {
        const string json = """
[
  {
    "boardId": 1,
    "mappingMode": "ReferenceOnly",
    "isDefault": true
  }
]
""";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var list = await BoardMappingRequestParser.ParseRequestBodyAsync(stream, JsonOptions);

        Assert.Single(list);
        Assert.Equal(1, list[0].BoardId);
        Assert.Equal(ExternalBoardMappingMode.ReferenceOnly, list[0].MappingMode);
        Assert.True(list[0].IsDefault);
    }

    [Fact]
    public async Task ParseRequestBody_WrapperObject_Parses()
    {
        const string json = """{"mappings":[{"boardId":2,"mappingMode":"Import","isDefault":false}]}""";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var list = await BoardMappingRequestParser.ParseRequestBodyAsync(stream, JsonOptions);

        Assert.Single(list);
        Assert.Equal(2, list[0].BoardId);
        Assert.Equal(ExternalBoardMappingMode.Import, list[0].MappingMode);
    }

    [Fact]
    public void ParseRoot_InvalidShape_ThrowsArgumentException()
    {
        using var doc = JsonDocument.Parse("""{"foo":[]}""");
        Assert.Throws<ArgumentException>(() => BoardMappingRequestParser.ParseRoot(doc.RootElement, JsonOptions));
    }

    [Fact]
    public void Deserialize_InvalidMappingModeString_ThrowsJsonException()
    {
        const string json = """[{"boardId":1,"mappingMode":"NotAMode","isDefault":false}]""";
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<List<ExternalBoardMappingItemRequest>>(json, JsonOptions));
    }

    [Fact]
    public async Task ReplaceBoardMappings_OneMapping_ReturnsList()
    {
        await using var context = CreateDbContext();
        SeedBoard(context);
        await context.SaveChangesAsync();
        var boardId = await context.TicketBoardDefinitions.Select(b => b.Id).FirstAsync();

        var service = IntegrationServiceTestFactory.Create(context);
        var connection = await service.CreateConnectionAsync(new CreateIntegrationConnectionRequest
        {
            Provider = IntegrationProvider.SharePoint,
            DisplayName = "C",
            TenantId = IntegrationConnectionTestDefaults.SharePointTenantId,
        });
        var source = await service.CreateSourceAsync(connection.Id, new CreateExternalWorkSourceRequest
        {
            Provider = IntegrationProvider.SharePoint,
            SourceType = ExternalSourceType.SharePointList,
            ExternalSourceId = "s",
            Name = "List",
        }) ?? throw new InvalidOperationException();

        var result = await service.ReplaceBoardMappingsAsync(source.Id,
        [
            new ExternalBoardMappingItemRequest
            {
                BoardId = boardId,
                MappingMode = ExternalBoardMappingMode.ReferenceOnly,
                IsDefault = true,
            },
        ]);

        Assert.NotNull(result);
        Assert.Single(result!);
        Assert.Equal(boardId, result[0].BoardId);
    }

    [Fact]
    public async Task ReplaceBoardMappings_GetReturnsSaved()
    {
        await using var context = CreateDbContext();
        SeedBoard(context);
        await context.SaveChangesAsync();
        var boardId = await context.TicketBoardDefinitions.Select(b => b.Id).FirstAsync();
        var boardName = await context.TicketBoardDefinitions.Where(b => b.Id == boardId).Select(b => b.Name).FirstAsync();

        var service = IntegrationServiceTestFactory.Create(context);
        var connection = await service.CreateConnectionAsync(new CreateIntegrationConnectionRequest
        {
            Provider = IntegrationProvider.SharePoint,
            DisplayName = "C",
            TenantId = IntegrationConnectionTestDefaults.SharePointTenantId,
        });
        var source = await service.CreateSourceAsync(connection.Id, new CreateExternalWorkSourceRequest
        {
            Provider = IntegrationProvider.SharePoint,
            SourceType = ExternalSourceType.SharePointList,
            ExternalSourceId = "x",
            Name = "L",
        }) ?? throw new InvalidOperationException();

        await service.ReplaceBoardMappingsAsync(source.Id,
        [
            new ExternalBoardMappingItemRequest { BoardId = boardId, MappingMode = ExternalBoardMappingMode.Mirror, IsDefault = false },
        ]);

        var fromService = await service.GetBoardMappingsAsync(source.Id);
        Assert.NotNull(fromService);
        Assert.Single(fromService!);
        Assert.Equal(boardName, fromService[0].BoardName);
        Assert.Equal(ExternalBoardMappingMode.Mirror, fromService[0].MappingMode);
    }

    [Fact]
    public async Task ReplaceBoardMappings_SecondPut_ReplacesNoDuplicates()
    {
        await using var context = CreateDbContext();
        SeedBoard(context);
        await context.SaveChangesAsync();
        var boardId = await context.TicketBoardDefinitions.Select(b => b.Id).FirstAsync();

        var service = IntegrationServiceTestFactory.Create(context);
        var connection = await service.CreateConnectionAsync(new CreateIntegrationConnectionRequest
        {
            Provider = IntegrationProvider.SharePoint,
            DisplayName = "C",
            TenantId = IntegrationConnectionTestDefaults.SharePointTenantId,
        });
        var source = await service.CreateSourceAsync(connection.Id, new CreateExternalWorkSourceRequest
        {
            Provider = IntegrationProvider.SharePoint,
            SourceType = ExternalSourceType.SharePointList,
            ExternalSourceId = "z",
            Name = "L",
        }) ?? throw new InvalidOperationException();

        await service.ReplaceBoardMappingsAsync(source.Id,
        [
            new ExternalBoardMappingItemRequest { BoardId = boardId, MappingMode = ExternalBoardMappingMode.Import, IsDefault = true },
            new ExternalBoardMappingItemRequest { BoardId = boardId, MappingMode = ExternalBoardMappingMode.ReferenceOnly, IsDefault = false },
        ]);

        await service.ReplaceBoardMappingsAsync(source.Id,
        [
            new ExternalBoardMappingItemRequest { BoardId = boardId, MappingMode = ExternalBoardMappingMode.ReferenceOnly, IsDefault = true },
        ]);

        Assert.Equal(1, await context.ExternalBoardMappings.CountAsync());
    }

    [Fact]
    public async Task ReplaceBoardMappings_MissingSource_ReturnsNull()
    {
        await using var context = CreateDbContext();
        SeedBoard(context);
        await context.SaveChangesAsync();
        var boardId = await context.TicketBoardDefinitions.Select(b => b.Id).FirstAsync();

        var service = IntegrationServiceTestFactory.Create(context);

        var result = await service.ReplaceBoardMappingsAsync(77123,
        [
            new ExternalBoardMappingItemRequest { BoardId = boardId, MappingMode = ExternalBoardMappingMode.ReferenceOnly, IsDefault = true },
        ]);

        Assert.Null(result);
    }

    [Fact]
    public async Task ReplaceBoardMappings_InvalidBoardId_ThrowsArgumentException_NotUnhandled()
    {
        await using var context = CreateDbContext();
        SeedBoard(context);
        await context.SaveChangesAsync();

        var service = IntegrationServiceTestFactory.Create(context);
        var connection = await service.CreateConnectionAsync(new CreateIntegrationConnectionRequest
        {
            Provider = IntegrationProvider.SharePoint,
            DisplayName = "C",
            TenantId = IntegrationConnectionTestDefaults.SharePointTenantId,
        });
        var source = await service.CreateSourceAsync(connection.Id, new CreateExternalWorkSourceRequest
        {
            Provider = IntegrationProvider.SharePoint,
            SourceType = ExternalSourceType.SharePointList,
            ExternalSourceId = "i",
            Name = "L",
        }) ?? throw new InvalidOperationException();

        await Assert.ThrowsAsync<ArgumentException>(() => service.ReplaceBoardMappingsAsync(source.Id,
        [
            new ExternalBoardMappingItemRequest { BoardId = -99999, MappingMode = ExternalBoardMappingMode.ReferenceOnly, IsDefault = false },
        ]));
    }

    [Fact]
    public async Task ReplaceBoardMappings_InvalidMappingModeNumeric_ThrowsArgumentException()
    {
        await using var context = CreateDbContext();
        SeedBoard(context);
        await context.SaveChangesAsync();
        var boardId = await context.TicketBoardDefinitions.Select(b => b.Id).FirstAsync();

        var service = IntegrationServiceTestFactory.Create(context);
        var connection = await service.CreateConnectionAsync(new CreateIntegrationConnectionRequest
        {
            Provider = IntegrationProvider.SharePoint,
            DisplayName = "C",
            TenantId = IntegrationConnectionTestDefaults.SharePointTenantId,
        });
        var source = await service.CreateSourceAsync(connection.Id, new CreateExternalWorkSourceRequest
        {
            Provider = IntegrationProvider.SharePoint,
            SourceType = ExternalSourceType.SharePointList,
            ExternalSourceId = "m",
            Name = "L",
        }) ?? throw new InvalidOperationException();

        await Assert.ThrowsAsync<ArgumentException>(() => service.ReplaceBoardMappingsAsync(source.Id,
        [
            new ExternalBoardMappingItemRequest
            {
                BoardId = boardId,
                MappingMode = (ExternalBoardMappingMode)999,
                IsDefault = false,
            },
        ]));
    }

    private static void SeedBoard(CortexDbContext context)
    {
        context.TicketBoardDefinitions.Add(new TicketBoardDefinition
        {
            Name = $"BoardMap-{Guid.NewGuid():N}",
            Description = "T",
            RequiresStoryPoints = false,
            IsEnabled = true,
            CreatedDateUtc = DateTime.UtcNow,
        });
    }

    private static CortexDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"boardmap-{Guid.NewGuid():N}")
            .Options;
        return new CortexDbContext(options);
    }
}
