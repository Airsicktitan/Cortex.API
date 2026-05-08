using Cortex.API.Database;
using Cortex.API.Infrastructure;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Tests;

public class SapReferenceDevCatalogSeedTests
{
    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"sap-dev-seed-{Guid.NewGuid():N}")
            .Options;
        return new CortexDbContext(options);
    }

    [Fact]
    public async Task EnsureAsync_ExistingMarcWithoutMatnr_AddsMatnr()
    {
        await using var db = CreateContext();
        var now = DateTime.UtcNow;
        var src = new SapReferenceSource
        {
            Name = "Pre-existing QA catalog",
            SourceType = SapReferenceSourceType.Manual,
            SystemLabel = "QA",
            IsEnabled = true,
            CreatedAtUtc = now,
        };
        db.SapReferenceSources.Add(src);
        await db.SaveChangesAsync();

        var marc = new SapTableMetadata
        {
            SapReferenceSourceId = src.Id,
            TableName = "MARC",
            Description = "Plant Data for Material",
            CreatedAtUtc = now,
        };
        db.SapTables.Add(marc);
        await db.SaveChangesAsync();

        db.SapFields.Add(new SapFieldMetadata
        {
            SapTableMetadataId = marc.Id,
            FieldName = "WERKS",
            Description = "Plant",
            IsKey = true,
            CreatedAtUtc = now,
        });
        await db.SaveChangesAsync();

        await SapReferenceDevCatalogSeed.EnsureAsync(db);

        var fields = await db.SapFields
            .Where(f => f.SapTableMetadataId == marc.Id)
            .Select(f => f.FieldName)
            .ToListAsync();

        Assert.Contains("MATNR", fields);
    }

    [Fact]
    public async Task EnsureAsync_MarcAlreadyHasMatnr_DoesNotDuplicate()
    {
        await using var db = CreateContext();
        var now = DateTime.UtcNow;
        var src = new SapReferenceSource
        {
            Name = "Pre-existing QA catalog",
            SourceType = SapReferenceSourceType.Manual,
            SystemLabel = "QA",
            IsEnabled = true,
            CreatedAtUtc = now,
        };
        db.SapReferenceSources.Add(src);
        await db.SaveChangesAsync();

        var marc = new SapTableMetadata
        {
            SapReferenceSourceId = src.Id,
            TableName = "MARC",
            CreatedAtUtc = now,
        };
        db.SapTables.Add(marc);
        await db.SaveChangesAsync();

        db.SapFields.Add(new SapFieldMetadata
        {
            SapTableMetadataId = marc.Id,
            FieldName = "MATNR",
            Description = "Material Number",
            IsKey = true,
            CreatedAtUtc = now,
        });
        await db.SaveChangesAsync();

        await SapReferenceDevCatalogSeed.EnsureAsync(db);

        var matnrCount = await db.SapFields
            .CountAsync(f => f.SapTableMetadataId == marc.Id && f.FieldName.ToUpper() == "MATNR");

        Assert.Equal(1, matnrCount);
    }
}
