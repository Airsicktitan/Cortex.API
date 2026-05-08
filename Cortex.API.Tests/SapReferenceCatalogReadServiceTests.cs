using Cortex.API.Database;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Tests;

public class SapReferenceCatalogReadServiceTests
{
    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"sap-catalog-read-{Guid.NewGuid():N}")
            .Options;
        return new CortexDbContext(options);
    }

    [Fact]
    public async Task ListAsync_TableWithFields_EmitsTableRowThenFieldRows()
    {
        await using var db = CreateContext();
        var now = DateTime.UtcNow;
        var src = new SapReferenceSource
        {
            Name = "Dev catalog",
            SourceType = SapReferenceSourceType.Manual,
            IsEnabled = true,
            CreatedAtUtc = now,
        };
        db.SapReferenceSources.Add(src);
        await db.SaveChangesAsync();

        var table = new SapTableMetadata
        {
            SapReferenceSourceId = src.Id,
            TableName = "MARC",
            Description = "Plant data",
            Module = "MM",
            BusinessObject = "Inventory",
            DataDomain = "Logistics",
            IsCustom = false,
            CreatedAtUtc = now,
        };
        db.SapTables.Add(table);
        await db.SaveChangesAsync();

        db.SapFields.Add(new SapFieldMetadata
        {
            SapTableMetadataId = table.Id,
            FieldName = "MATNR",
            Description = "Material number",
            IsKey = true,
            IsRequired = true,
            IsCustom = false,
            CreatedAtUtc = now,
        });
        db.SapFields.Add(new SapFieldMetadata
        {
            SapTableMetadataId = table.Id,
            FieldName = "YYNGM_ACTIVE",
            Description = "Custom flag",
            IsKey = false,
            IsRequired = false,
            IsCustom = false,
            CreatedAtUtc = now,
        });
        await db.SaveChangesAsync();

        var svc = new SapReferenceCatalogReadService(db);
        var result = await svc.ListAsync(null);

        Assert.Equal(3, result.Entries.Count);
        Assert.Equal("Table", result.Entries[0].RowKind);
        Assert.Equal("MARC", result.Entries[0].TableName);
        Assert.Equal(2, result.Entries[0].FieldCount);
        Assert.Contains(result.Entries, e =>
            e.RowKind == "Field" && e.FieldName == "MATNR" && e.IsKey == true);
        var yy = result.Entries.Single(e => e.FieldName == "YYNGM_ACTIVE");
        Assert.True(yy.LikelyCustomSapField);
    }

    [Fact]
    public async Task ListAsync_TableWithoutFields_TableRowOnly()
    {
        await using var db = CreateContext();
        var now = DateTime.UtcNow;
        var src = new SapReferenceSource
        {
            Name = "Src",
            IsEnabled = true,
            CreatedAtUtc = now,
        };
        db.SapReferenceSources.Add(src);
        await db.SaveChangesAsync();
        db.SapTables.Add(new SapTableMetadata
        {
            SapReferenceSourceId = src.Id,
            TableName = "EMPTY",
            Description = "No fields",
            CreatedAtUtc = now,
        });
        await db.SaveChangesAsync();

        var svc = new SapReferenceCatalogReadService(db);
        var result = await svc.ListAsync(null);

        Assert.Single(result.Entries);
        Assert.Equal("Table", result.Entries[0].RowKind);
        Assert.Equal(0, result.Entries[0].FieldCount);
    }

    [Fact]
    public async Task ListAsync_SourceType_FormattedForDisplay()
    {
        await using var db = CreateContext();
        var now = DateTime.UtcNow;
        var src = new SapReferenceSource
        {
            Name = "Import",
            SourceType = SapReferenceSourceType.CsvImport,
            IsEnabled = false,
            CreatedAtUtc = now,
        };
        db.SapReferenceSources.Add(src);
        await db.SaveChangesAsync();
        db.SapTables.Add(new SapTableMetadata
        {
            SapReferenceSourceId = src.Id,
            TableName = "T001",
            CreatedAtUtc = now,
        });
        await db.SaveChangesAsync();

        var svc = new SapReferenceCatalogReadService(db);
        var result = await svc.ListAsync(null);

        Assert.Equal("File import", result.Entries[0].SourceType);
        Assert.False(result.Entries[0].SourceIsEnabled);
    }

    [Fact]
    public async Task ListAsync_SearchMarc_OrdersTableBeforeFieldsAndWeakDescription()
    {
        await using var db = CreateContext();
        var now = DateTime.UtcNow;
        var src = new SapReferenceSource
        {
            Name = "S",
            IsEnabled = true,
            CreatedAtUtc = now,
        };
        db.SapReferenceSources.Add(src);
        await db.SaveChangesAsync();

        var marc = new SapTableMetadata
        {
            SapReferenceSourceId = src.Id,
            TableName = "MARC",
            Description = "Plant data",
            CreatedAtUtc = now,
        };
        var ztemp = new SapTableMetadata
        {
            SapReferenceSourceId = src.Id,
            TableName = "ZTEMP",
            Description = "Like MARC staging table",
            CreatedAtUtc = now,
        };
        db.SapTables.AddRange(marc, ztemp);
        await db.SaveChangesAsync();

        db.SapFields.Add(new SapFieldMetadata
        {
            SapTableMetadataId = marc.Id,
            FieldName = "WERKS",
            Description = "Plant",
            CreatedAtUtc = now,
        });
        await db.SaveChangesAsync();

        var svc = new SapReferenceCatalogReadService(db);
        var result = await svc.ListAsync("MARC");

        Assert.True(result.Entries.Count >= 3);
        Assert.Equal("Table", result.Entries[0].RowKind);
        Assert.Equal("MARC", result.Entries[0].TableName);
        var list = result.Entries.ToList();
        var werksIdx = list.FindIndex(e => e.RowKind == "Field" && e.FieldName == "WERKS");
        var zIdx = list.FindIndex(e => e.TableName == "ZTEMP");
        Assert.True(werksIdx >= 0 && zIdx >= 0);
        Assert.True(werksIdx < zIdx);
    }

    [Fact]
    public async Task ListAsync_SearchMatnr_FieldRowBeforeDescriptionOnly()
    {
        await using var db = CreateContext();
        var now = DateTime.UtcNow;
        var src = new SapReferenceSource
        {
            Name = "S",
            IsEnabled = true,
            CreatedAtUtc = now,
        };
        db.SapReferenceSources.Add(src);
        await db.SaveChangesAsync();

        var marc = new SapTableMetadata
        {
            SapReferenceSourceId = src.Id,
            TableName = "MARC",
            Description = "Plant data",
            CreatedAtUtc = now,
        };
        var other = new SapTableMetadata
        {
            SapReferenceSourceId = src.Id,
            TableName = "TALK",
            Description = "Talk",
            CreatedAtUtc = now,
        };
        db.SapTables.AddRange(marc, other);
        await db.SaveChangesAsync();

        db.SapFields.Add(new SapFieldMetadata
        {
            SapTableMetadataId = marc.Id,
            FieldName = "MATNR",
            Description = "Material number",
            CreatedAtUtc = now,
        });
        db.SapFields.Add(new SapFieldMetadata
        {
            SapTableMetadataId = other.Id,
            FieldName = "DOCNO",
            Description = "See MATNR in docs",
            CreatedAtUtc = now,
        });
        await db.SaveChangesAsync();

        var svc = new SapReferenceCatalogReadService(db);
        var result = await svc.ListAsync("MATNR");

        Assert.True(result.Entries.Count >= 2);
        Assert.Equal("Field", result.Entries[0].RowKind);
        Assert.Equal("MATNR", result.Entries[0].FieldName);
    }

    [Fact]
    public async Task ListAsync_SearchYyngmActive_FieldFirst()
    {
        await using var db = CreateContext();
        var now = DateTime.UtcNow;
        var src = new SapReferenceSource
        {
            Name = "S",
            IsEnabled = true,
            CreatedAtUtc = now,
        };
        db.SapReferenceSources.Add(src);
        await db.SaveChangesAsync();

        var marc = new SapTableMetadata
        {
            SapReferenceSourceId = src.Id,
            TableName = "MARC",
            Description = "Plant data",
            CreatedAtUtc = now,
        };
        var other = new SapTableMetadata
        {
            SapReferenceSourceId = src.Id,
            TableName = "ZOTH",
            Description = "Mentions YYNGM_ACTIVE flag",
            CreatedAtUtc = now,
        };
        db.SapTables.AddRange(marc, other);
        await db.SaveChangesAsync();

        db.SapFields.Add(new SapFieldMetadata
        {
            SapTableMetadataId = marc.Id,
            FieldName = "YYNGM_ACTIVE",
            Description = "Flag",
            IsCustom = false,
            CreatedAtUtc = now,
        });
        await db.SaveChangesAsync();

        var svc = new SapReferenceCatalogReadService(db);
        var result = await svc.ListAsync("YYNGM_ACTIVE");

        Assert.True(result.Entries.Count >= 1);
        Assert.Equal("Field", result.Entries[0].RowKind);
        Assert.Equal("YYNGM_ACTIVE", result.Entries[0].FieldName);
        Assert.True(result.Entries[0].LikelyCustomSapField);
    }
}
