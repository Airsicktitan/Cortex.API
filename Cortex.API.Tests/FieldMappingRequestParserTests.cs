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

public class FieldMappingRequestParserTests
{
    private static JsonSerializerOptions JsonOptions { get; } = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task ParseRequestBody_Array_MatchesQaShape()
    {
        const string json = """
[
  {
    "externalFieldName": "Title",
    "externalFieldKey": "Title",
    "cortexField": "Title",
    "isRequired": true,
    "transformHint": null
  },
  {
    "externalFieldName": "Request Details",
    "externalFieldKey": "RequestDetails",
    "cortexField": "Description",
    "isRequired": false,
    "transformHint": null
  },
  {
    "externalFieldName": "Urgency",
    "externalFieldKey": "Urgency",
    "cortexField": "Priority",
    "isRequired": false,
    "transformHint": "Map Low/Medium/High/Critical to Cortex priorities"
  },
  {
    "externalFieldName": "Assigned Consultant",
    "externalFieldKey": "AssignedConsultant",
    "cortexField": "SynitiOwner",
    "isRequired": false,
    "transformHint": null
  },
  {
    "externalFieldName": "Business Owner",
    "externalFieldKey": "BusinessOwner",
    "cortexField": "BusinessOwner",
    "isRequired": false,
    "transformHint": null
  }
]
""";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var list = await FieldMappingRequestParser.ParseRequestBodyAsync(stream, JsonOptions);

        Assert.Equal(5, list.Count);
        Assert.Equal("Urgency", list[2].ExternalFieldName);
        Assert.Equal(CortexField.SynitiOwner, list[3].CortexField);
    }

    [Fact]
    public async Task ParseRequestBody_ObjectWithMappingsProperty_Works()
    {
        const string json = """{"mappings":[{"externalFieldName":"A","cortexField":"Title","isRequired":false}]}""";
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        var list = await FieldMappingRequestParser.ParseRequestBodyAsync(stream, JsonOptions);

        Assert.Single(list);
        Assert.Equal("A", list[0].ExternalFieldName);
    }

    [Fact]
    public void ParseRoot_InvalidShape_ThrowsArgumentException()
    {
        using var doc = JsonDocument.Parse("""{"foo":[]}""");
        Assert.Throws<ArgumentException>(() => FieldMappingRequestParser.ParseRoot(doc.RootElement, JsonOptions));
    }

    [Fact]
    public void Deserialize_InvalidCortexFieldString_ThrowsJsonException()
    {
        const string json = """[{"externalFieldName":"x","cortexField":"NotARealField","isRequired":false}]""";
        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<List<ExternalFieldMappingItemRequest>>(json, JsonOptions));
    }

    [Fact]
    public async Task ReplaceFieldMappings_OneMapping_ReturnsOkList()
    {
        await using var context = CreateDbContext();
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

        var result = await service.ReplaceFieldMappingsAsync(source.Id,
        [
            new ExternalFieldMappingItemRequest
            {
                ExternalFieldName = "Only",
                CortexField = CortexField.Title,
                IsRequired = true,
            },
        ]);

        Assert.NotNull(result);
        Assert.Single(result!);
    }

    [Fact]
    public async Task ReplaceFieldMappings_FiveMappings_GetReturnsSame()
    {
        await using var context = CreateDbContext();
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
            ExternalSourceId = "list",
            Name = "SAP Support Requests",
        }) ?? throw new InvalidOperationException();

        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(QaPayloadJson));
        var parsed = await FieldMappingRequestParser.ParseRequestBodyAsync(stream, JsonOptions);

        await service.ReplaceFieldMappingsAsync(source.Id, parsed);

        var stored = await service.GetFieldMappingsAsync(source.Id);
        Assert.NotNull(stored);
        Assert.Equal(5, stored!.Count);

        var fromDb = await context.ExternalFieldMappings.AsNoTracking()
            .Where(m => m.ExternalWorkSourceId == source.Id)
            .ToListAsync();
        Assert.Equal(5, fromDb.Count);
        Assert.All(fromDb, m => Assert.Equal(source.Id, m.ExternalWorkSourceId));
        Assert.Contains(fromDb, m => m.ExternalFieldName == "Request Details" && m.CortexField == CortexField.Description);
    }

    [Fact]
    public async Task ReplaceFieldMappings_SecondPut_ReplacesWithoutDuplicates()
    {
        await using var context = CreateDbContext();
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

        await service.ReplaceFieldMappingsAsync(source.Id,
        [
            new ExternalFieldMappingItemRequest { ExternalFieldName = "A", CortexField = CortexField.Title, IsRequired = false },
            new ExternalFieldMappingItemRequest { ExternalFieldName = "B", CortexField = CortexField.Description, IsRequired = false },
        ]);

        await service.ReplaceFieldMappingsAsync(source.Id,
        [
            new ExternalFieldMappingItemRequest { ExternalFieldName = "Only", CortexField = CortexField.Status, IsRequired = true },
        ]);

        Assert.Equal(1, await context.ExternalFieldMappings.CountAsync());
        var row = await context.ExternalFieldMappings.SingleAsync();
        Assert.Equal("Only", row.ExternalFieldName);
        Assert.Equal(CortexField.Status, row.CortexField);
    }

    [Fact]
    public async Task ReplaceFieldMappings_MissingSource_ReturnsNull()
    {
        await using var context = CreateDbContext();
        var service = IntegrationServiceTestFactory.Create(context);

        var result = await service.ReplaceFieldMappingsAsync(91919,
        [
            new ExternalFieldMappingItemRequest { ExternalFieldName = "A", CortexField = CortexField.Title, IsRequired = false },
        ]);

        Assert.Null(result);
    }

    [Fact]
    public async Task ReplaceFieldMappings_InvalidEnumValue_ThrowsArgumentException()
    {
        await using var context = CreateDbContext();
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

        await Assert.ThrowsAsync<ArgumentException>(() => service.ReplaceFieldMappingsAsync(source.Id,
        [
            new ExternalFieldMappingItemRequest
            {
                ExternalFieldName = "Bad",
                CortexField = (CortexField)999,
                IsRequired = false,
            },
        ]));
    }

    private static CortexDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"fieldmap-{Guid.NewGuid():N}")
            .Options;
        return new CortexDbContext(options);
    }

    private const string QaPayloadJson = """
[
  {
    "externalFieldName": "Title",
    "externalFieldKey": "Title",
    "cortexField": "Title",
    "isRequired": true,
    "transformHint": null
  },
  {
    "externalFieldName": "Request Details",
    "externalFieldKey": "RequestDetails",
    "cortexField": "Description",
    "isRequired": false,
    "transformHint": null
  },
  {
    "externalFieldName": "Urgency",
    "externalFieldKey": "Urgency",
    "cortexField": "Priority",
    "isRequired": false,
    "transformHint": "Map Low/Medium/High/Critical to Cortex priorities"
  },
  {
    "externalFieldName": "Assigned Consultant",
    "externalFieldKey": "AssignedConsultant",
    "cortexField": "SynitiOwner",
    "isRequired": false,
    "transformHint": null
  },
  {
    "externalFieldName": "Business Owner",
    "externalFieldKey": "BusinessOwner",
    "cortexField": "BusinessOwner",
    "isRequired": false,
    "transformHint": null
  }
]
""";
}
