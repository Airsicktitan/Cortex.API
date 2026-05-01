using System.Text.Json;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Tests;

public class SapReferenceServiceTests
{
    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"sap-ref-{Guid.NewGuid():N}")
            .Options;
        return new CortexDbContext(options);
    }

    [Fact]
    public async Task CreateSource_Persists()
    {
        await using var ctx = CreateContext();
        var svc = new SapReferenceService(ctx);
        var created = await svc.CreateSourceAsync(new CreateSapReferenceSourceRequest(
            "Corp SAP dictionary",
            "QA",
            SapReferenceSourceType.Manual,
            "PRD",
            "100",
            "Production",
            true));
        Assert.True(created.Id > 0);
        Assert.Equal("Corp SAP dictionary", created.Name);
    }

    [Fact]
    public async Task CreateTable_OnSource_Persists()
    {
        await using var ctx = CreateContext();
        var svc = new SapReferenceService(ctx);
        var src = await svc.CreateSourceAsync(new CreateSapReferenceSourceRequest("S", null, null, null, null, null, true));
        var table = await svc.CreateTableAsync(src.Id, new CreateSapTableMetadataRequest(
            "marc",
            "Plant data",
            "MM",
            "Material Master",
            null,
            false,
            null));
        Assert.NotNull(table);
        Assert.Equal("MARC", table!.TableName);
    }

    [Fact]
    public async Task CreateField_OnTable_Persists()
    {
        await using var ctx = CreateContext();
        var svc = new SapReferenceService(ctx);
        var src = await svc.CreateSourceAsync(new CreateSapReferenceSourceRequest("S", null, null, null, null, null, true));
        var table = await svc.CreateTableAsync(src.Id, new CreateSapTableMetadataRequest("MARC", null, null, null, null, null, null));
        var field = await svc.CreateFieldAsync(table!.Id, new CreateSapFieldMetadataRequest(
            "werks",
            "Plant",
            null,
            null,
            "CHAR",
            4,
            true,
            null,
            null,
            null,
            "1000",
            null));
        Assert.NotNull(field);
        Assert.Equal("WERKS", field!.FieldName);
    }

    [Fact]
    public async Task CreateField_YyPrefix_DefaultsIsCustomTrue()
    {
        await using var ctx = CreateContext();
        var svc = new SapReferenceService(ctx);
        var src = await svc.CreateSourceAsync(new CreateSapReferenceSourceRequest("S", null, null, null, null, null, true));
        var table = await svc.CreateTableAsync(src.Id, new CreateSapTableMetadataRequest("MARC", null, null, null, null, null, null));
        var field = await svc.CreateFieldAsync(table!.Id, new CreateSapFieldMetadataRequest(
            "yYNGM_ACTIVE",
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null));
        Assert.NotNull(field);
        Assert.True(field!.IsCustom);
    }

    [Fact]
    public async Task Search_FindsTable_ByTableName()
    {
        await using var ctx = CreateContext();
        var svc = new SapReferenceService(ctx);
        var src = await svc.CreateSourceAsync(new CreateSapReferenceSourceRequest("S", null, null, null, null, null, true));
        await svc.CreateTableAsync(src.Id, new CreateSapTableMetadataRequest("MARA", "General Material Data", null, null, null, null, null));
        var hits = await svc.SearchAsync("MARA");
        Assert.Contains(hits, h => h.ResultType == "Table" && h.TableName == "MARA");
    }

    [Fact]
    public async Task Search_FindsField_ByFieldName()
    {
        await using var ctx = CreateContext();
        var svc = new SapReferenceService(ctx);
        var src = await svc.CreateSourceAsync(new CreateSapReferenceSourceRequest("S", null, null, null, null, null, true));
        var table = await svc.CreateTableAsync(src.Id, new CreateSapTableMetadataRequest("MARC", null, null, null, null, null, null));
        await svc.CreateFieldAsync(table!.Id, new CreateSapFieldMetadataRequest("YYNGM_ACTIVE", null, null, null, null, null, null, null, null, null, null, null));
        var hits = await svc.SearchAsync("YYNGM_ACTIVE");
        Assert.Contains(hits, h => h.ResultType == "Field" && h.FieldName == "YYNGM_ACTIVE");
    }

    [Fact]
    public async Task Search_FindsTable_ByBusinessObject()
    {
        await using var ctx = CreateContext();
        var svc = new SapReferenceService(ctx);
        var src = await svc.CreateSourceAsync(new CreateSapReferenceSourceRequest("S", null, null, null, null, null, true));
        await svc.CreateTableAsync(src.Id, new CreateSapTableMetadataRequest("MARC", null, "MM", "Material Master", null, null, null));
        var hits = await svc.SearchAsync("Material Master");
        Assert.Contains(hits, h => h.ResultType == "Table" && h.BusinessObject == "Material Master");
    }

    [Fact]
    public void SearchResult_Serialization_HasNoSecretsPayload()
    {
        var dto = new SapReferenceSearchResultDto(
            "Field",
            1,
            "Src",
            2,
            "MARC",
            3,
            "YYNGM_ACTIVE",
            "YYNGM_ACTIVE",
            "Field on MARC",
            "desc",
            true,
            "MM",
            "Material Master",
            "Matched field name",
            null);
        var json = JsonSerializer.Serialize(dto);
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RawJson", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DuplicateTable_SameSource_Throws()
    {
        await using var ctx = CreateContext();
        var svc = new SapReferenceService(ctx);
        var src = await svc.CreateSourceAsync(new CreateSapReferenceSourceRequest("S", null, null, null, null, null, true));
        await svc.CreateTableAsync(src.Id, new CreateSapTableMetadataRequest("MARC", null, null, null, null, null, null));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateTableAsync(src.Id, new CreateSapTableMetadataRequest("marc", null, null, null, null, null, null)));
    }

    [Fact]
    public async Task DuplicateSource_SameName_Throws()
    {
        await using var ctx = CreateContext();
        var svc = new SapReferenceService(ctx);
        await svc.CreateSourceAsync(new CreateSapReferenceSourceRequest("Duplicate", null, null, null, null, null, true));
        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.CreateSourceAsync(new CreateSapReferenceSourceRequest("duplicate", null, null, null, null, null, true)));
    }

    [Fact]
    public async Task DeleteSource_RemovesSourceAndCascadeChildren()
    {
        await using var ctx = CreateContext();
        var svc = new SapReferenceService(ctx);
        var src = await svc.CreateSourceAsync(new CreateSapReferenceSourceRequest("S", null, null, null, null, null, true));
        var table = await svc.CreateTableAsync(src.Id, new CreateSapTableMetadataRequest("MARC", null, null, null, null, null, null));
        await svc.CreateFieldAsync(table!.Id, new CreateSapFieldMetadataRequest("WERKS", null, null, null, null, null, null, null, null, null, null, null));
        await svc.CreateDomainValueAsync(src.Id, new CreateSapDomainValueRequest("DOM", "01", null, null));

        Assert.True(await svc.DeleteSourceAsync(src.Id));
        Assert.False(await ctx.SapReferenceSources.AnyAsync(s => s.Id == src.Id));
        Assert.False(await ctx.SapTables.AnyAsync());
        Assert.False(await ctx.SapFields.AnyAsync());
        Assert.False(await ctx.SapDomainValues.AnyAsync());
    }

    [Fact]
    public async Task DeleteSource_Missing_ReturnsFalse()
    {
        await using var ctx = CreateContext();
        var svc = new SapReferenceService(ctx);
        Assert.False(await svc.DeleteSourceAsync(999));
    }

    [Fact]
    public async Task DeleteTable_RemovesTableAndFields()
    {
        await using var ctx = CreateContext();
        var svc = new SapReferenceService(ctx);
        var src = await svc.CreateSourceAsync(new CreateSapReferenceSourceRequest("S", null, null, null, null, null, true));
        var table = await svc.CreateTableAsync(src.Id, new CreateSapTableMetadataRequest("MARC", null, null, null, null, null, null));
        await svc.CreateFieldAsync(table!.Id, new CreateSapFieldMetadataRequest("WERKS", null, null, null, null, null, null, null, null, null, null, null));
        await svc.CreateFieldAsync(table.Id, new CreateSapFieldMetadataRequest("MATNR", null, null, null, null, null, null, null, null, null, null, null));

        Assert.True(await svc.DeleteTableAsync(table.Id));
        Assert.False(await ctx.SapTables.AnyAsync(t => t.Id == table.Id));
        Assert.False(await ctx.SapFields.AnyAsync());
    }

    [Fact]
    public async Task DeleteTable_Missing_ReturnsFalse()
    {
        await using var ctx = CreateContext();
        var svc = new SapReferenceService(ctx);
        Assert.False(await svc.DeleteTableAsync(999));
    }

    [Fact]
    public async Task DeleteField_RemovesOnlyThatField()
    {
        await using var ctx = CreateContext();
        var svc = new SapReferenceService(ctx);
        var src = await svc.CreateSourceAsync(new CreateSapReferenceSourceRequest("S", null, null, null, null, null, true));
        var table = await svc.CreateTableAsync(src.Id, new CreateSapTableMetadataRequest("MARC", null, null, null, null, null, null));
        var kee = await svc.CreateFieldAsync(table!.Id, new CreateSapFieldMetadataRequest("WERKS", null, null, null, null, null, null, null, null, null, null, null));
        var drop = await svc.CreateFieldAsync(table.Id, new CreateSapFieldMetadataRequest("MATNR", null, null, null, null, null, null, null, null, null, null, null));

        Assert.True(await svc.DeleteFieldAsync(drop!.Id));
        Assert.True(await ctx.SapFields.AnyAsync(f => f.Id == kee!.Id));
        Assert.False(await ctx.SapFields.AnyAsync(f => f.Id == drop.Id));
    }

    [Fact]
    public async Task DeleteField_Missing_ReturnsFalse()
    {
        await using var ctx = CreateContext();
        var svc = new SapReferenceService(ctx);
        Assert.False(await svc.DeleteFieldAsync(999));
    }

    [Fact]
    public async Task DeleteDomainValue_Missing_ReturnsFalse()
    {
        await using var ctx = CreateContext();
        var svc = new SapReferenceService(ctx);
        Assert.False(await svc.DeleteDomainValueAsync(999));
    }

    [Fact]
    public async Task DeleteDomainValue_RemovesRow()
    {
        await using var ctx = CreateContext();
        var svc = new SapReferenceService(ctx);
        var src = await svc.CreateSourceAsync(new CreateSapReferenceSourceRequest("S", null, null, null, null, null, true));
        var d = await svc.CreateDomainValueAsync(src.Id, new CreateSapDomainValueRequest("DOM", "X", null, null));
        Assert.True(await svc.DeleteDomainValueAsync(d!.Id));
        Assert.False(await ctx.SapDomainValues.AnyAsync(x => x.Id == d.Id));
    }

    [Fact]
    public async Task Search_NoLongerReturns_DeletedField()
    {
        await using var ctx = CreateContext();
        var svc = new SapReferenceService(ctx);
        var src = await svc.CreateSourceAsync(new CreateSapReferenceSourceRequest("S", null, null, null, null, null, true));
        var table = await svc.CreateTableAsync(src.Id, new CreateSapTableMetadataRequest("MARC", null, null, null, null, null, null));
        var field = await svc.CreateFieldAsync(table!.Id, new CreateSapFieldMetadataRequest("ZZTEST_SRCH", null, null, null, null, null, null, null, null, null, null, null));

        var before = await svc.SearchAsync("ZZTEST_SRCH");
        Assert.Contains(before, h => h.FieldId == field!.Id);

        await svc.DeleteFieldAsync(field!.Id);
        var after = await svc.SearchAsync("ZZTEST_SRCH");
        Assert.DoesNotContain(after, h => h.FieldId == field.Id);
    }
}
