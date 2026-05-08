using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public interface ISapReferenceCatalogReadService
{
    Task<SapReferenceCatalogListResponse> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>Read-only SAP table/field rows for admin/catalog visibility.</summary>
public sealed class SapReferenceCatalogReadService(CortexDbContext db) : ISapReferenceCatalogReadService
{
    public async Task<SapReferenceCatalogListResponse> ListAsync(CancellationToken cancellationToken = default)
    {
        var tables = await db.SapTables.AsNoTracking()
            .Include(t => t.SapReferenceSource)
            .Include(t => t.Fields)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var entries = new List<SapReferenceCatalogEntryDto>(capacity: tables.Count * 2);

        foreach (var table in tables.OrderBy(t => t.TableName, StringComparer.OrdinalIgnoreCase))
        {
            var source = table.SapReferenceSource;
            var sourceName = source.Name.Trim();
            var sourceType = FormatSourceType(source.SourceType);
            var fieldList = table.Fields
                .OrderBy(f => f.FieldName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (fieldList.Count == 0)
            {
                entries.Add(BuildTableRow(table, source, sourceName, sourceType, fieldCount: 0));
            }
            else
            {
                entries.Add(BuildTableRow(table, source, sourceName, sourceType, fieldList.Count));
                foreach (var field in fieldList)
                {
                    entries.Add(BuildFieldRow(table, field, source, sourceName, sourceType));
                }
            }
        }

        return new SapReferenceCatalogListResponse(entries);
    }

    private static SapReferenceCatalogEntryDto BuildTableRow(
        SapTableMetadata table,
        SapReferenceSource source,
        string sourceName,
        string sourceType,
        int fieldCount)
    {
        var desc = TrimOrNull(table.Description);
        return new SapReferenceCatalogEntryDto(
            RowKind: "Table",
            TableName: table.TableName.Trim(),
            FieldName: null,
            TableDescription: desc,
            FieldDescription: null,
            BusinessObject: TrimOrNull(table.BusinessObject),
            Module: TrimOrNull(table.Module),
            Domain: TrimOrNull(table.DataDomain),
            IsKey: null,
            IsRequired: null,
            IsCustomField: table.IsCustom,
            LikelyCustomSapField: table.IsCustom,
            SourceName: sourceName,
            SourceType: sourceType,
            SourceIsEnabled: source.IsEnabled,
            FieldCount: fieldCount,
            CreatedAtUtc: table.CreatedAtUtc,
            UpdatedAtUtc: table.UpdatedAtUtc);
    }

    private static SapReferenceCatalogEntryDto BuildFieldRow(
        SapTableMetadata table,
        SapFieldMetadata field,
        SapReferenceSource source,
        string sourceName,
        string sourceType)
    {
        var tableDesc = TrimOrNull(table.Description);
        var fieldDesc = TrimOrNull(field.Description);
        var likelyCustom = SapTicketReferenceDetector.IsLikelyCustomerExtension(field.FieldName, field.IsCustom);

        return new SapReferenceCatalogEntryDto(
            RowKind: "Field",
            TableName: table.TableName.Trim(),
            FieldName: field.FieldName.Trim(),
            TableDescription: tableDesc,
            FieldDescription: fieldDesc,
            BusinessObject: TrimOrNull(table.BusinessObject),
            Module: TrimOrNull(table.Module),
            Domain: TrimOrNull(table.DataDomain),
            IsKey: field.IsKey,
            IsRequired: field.IsRequired,
            IsCustomField: field.IsCustom,
            LikelyCustomSapField: likelyCustom,
            SourceName: sourceName,
            SourceType: sourceType,
            SourceIsEnabled: source.IsEnabled,
            FieldCount: 0,
            CreatedAtUtc: field.CreatedAtUtc,
            UpdatedAtUtc: field.UpdatedAtUtc);
    }

    private static string? TrimOrNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string FormatSourceType(SapReferenceSourceType t) => t switch
    {
        SapReferenceSourceType.Manual => "Manual entry",
        SapReferenceSourceType.CsvImport => "File import",
        SapReferenceSourceType.MetadataExport => "Metadata export",
        SapReferenceSourceType.SynitiExport => "Syniti export",
        SapReferenceSourceType.FutureLiveSap => "Reserved",
        _ => t.ToString(),
    };
}
