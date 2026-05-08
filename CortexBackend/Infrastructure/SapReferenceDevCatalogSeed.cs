using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Infrastructure;

/// <summary>
/// Development-only demo SAP reference rows when the catalog is empty, plus idempotent
/// enrichment so existing local databases pick up standard MARC fields (e.g. MATNR) without a reset.
/// </summary>
public static class SapReferenceDevCatalogSeed
{
    public static async Task EnsureAsync(CortexDbContext db, CancellationToken cancellationToken = default)
    {
        if (!await db.SapReferenceSources.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            await SeedDemoCatalogWhenEmptyAsync(db, cancellationToken).ConfigureAwait(false);
        }

        await EnsureMarcMatnrOnAllMarcTablesAsync(db, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Ensures MATNR exists on every MARC table row (any catalog source) — fixes older dev DBs seeded before MATNR existed.
    /// </summary>
    private static async Task EnsureMarcMatnrOnAllMarcTablesAsync(
        CortexDbContext db,
        CancellationToken cancellationToken)
    {
        var marcTableIds = await db.SapTables.AsNoTracking()
            .Where(t => t.TableName.ToUpper() == "MARC")
            .Select(t => t.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (marcTableIds.Count == 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var added = false;

        foreach (var tableId in marcTableIds)
        {
            var hasMatnr = await db.SapFields.AnyAsync(
                    f => f.SapTableMetadataId == tableId && f.FieldName.ToUpper() == "MATNR",
                    cancellationToken)
                .ConfigureAwait(false);

            if (hasMatnr)
            {
                continue;
            }

            db.SapFields.Add(new SapFieldMetadata
            {
                SapTableMetadataId = tableId,
                FieldName = "MATNR",
                Description = "Material Number",
                IsKey = true,
                IsCustom = false,
                BusinessMeaning = "Material identifier at plant level",
                CreatedAtUtc = now,
            });
            added = true;
        }

        if (added)
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task SeedDemoCatalogWhenEmptyAsync(
        CortexDbContext db,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var src = new SapReferenceSource
        {
            Name = "Demo SAP reference (local)",
            Description = "Sample table/field metadata for QA. Not connected to a live SAP system.",
            SourceType = SapReferenceSourceType.Manual,
            SystemLabel = "DEV",
            IsEnabled = true,
            CreatedAtUtc = now,
        };
        db.SapReferenceSources.Add(src);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        static SapTableMetadata T(
            int sourceId,
            string table,
            string? desc,
            string? module,
            string? bo,
            bool custom,
            DateTime utc) =>
            new()
            {
                SapReferenceSourceId = sourceId,
                TableName = table,
                Description = desc,
                Module = module,
                BusinessObject = bo,
                IsCustom = custom,
                CreatedAtUtc = utc,
            };

        var tables = new[]
        {
            T(src.Id, "MARA", "General Material Data", "MM", "Material Master", false, now),
            T(src.Id, "MARC", "Plant Data for Material", "MM", "Material Master", false, now),
            T(src.Id, "LFA1", "Vendor Master (General Section)", "FI", "Vendor Master", false, now),
            T(src.Id, "KNA1", "Customer Master (General Data)", "SD", "Customer Master", false, now),
            T(src.Id, "QMAT", "Inspection Type - Material Parameters", "QM", "Quality Management", false, now),
            T(
                src.Id,
                "EINA",
                "Purchasing Info Record: General Data",
                "MM",
                "Purchasing Info Record",
                false,
                now),
            T(
                src.Id,
                "EINE",
                "Purchasing Info Record: Organization Data",
                "MM",
                "Purchasing Info Record",
                false,
                now),
        };
        foreach (var t in tables)
        {
            db.SapTables.Add(t);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var marc = await db.SapTables.AsNoTracking().FirstAsync(
                x => x.SapReferenceSourceId == src.Id && x.TableName == "MARC",
                cancellationToken)
            .ConfigureAwait(false);
        var lfa1 = await db.SapTables.AsNoTracking().FirstAsync(
                x => x.SapReferenceSourceId == src.Id && x.TableName == "LFA1",
                cancellationToken)
            .ConfigureAwait(false);
        var kna1 = await db.SapTables.AsNoTracking().FirstAsync(
                x => x.SapReferenceSourceId == src.Id && x.TableName == "KNA1",
                cancellationToken)
            .ConfigureAwait(false);

        SapFieldMetadata F(
            int tableId,
            string name,
            string? desc,
            bool key,
            bool custom,
            string? meaning) =>
            new()
            {
                SapTableMetadataId = tableId,
                FieldName = name,
                Description = desc,
                IsKey = key,
                IsCustom = custom,
                BusinessMeaning = meaning,
                CreatedAtUtc = now,
            };

        db.SapFields.Add(F(marc.Id, "WERKS", "Plant", true, false, "Plant code"));
        db.SapFields.Add(F(marc.Id, "MATNR", "Material Number", true, false, "Material identifier at plant level"));
        db.SapFields.Add(F(marc.Id, "MMSTA", "Plant-specific material status", false, false, null));
        db.SapFields.Add(F(marc.Id, "YYNGM_ACTIVE", "Custom active flag (example)", false, true, "Active flag for NGM process"));
        db.SapFields.Add(F(lfa1.Id, "LIFNR", "Vendor account number", true, false, null));
        db.SapFields.Add(F(kna1.Id, "KUNNR", "Customer number", true, false, null));
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
