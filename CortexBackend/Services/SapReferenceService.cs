using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public sealed class SapReferenceService(CortexDbContext db) : ISapReferenceService
{
    private const int SearchMaxResults = 80;

    public async Task<IReadOnlyList<SapReferenceSourceResponse>> ListSourcesAsync(CancellationToken cancellationToken = default)
    {
        var rows = await db.SapReferenceSources.AsNoTracking()
            .OrderBy(s => s.Name)
            .ToListAsync(cancellationToken);
        return rows.ConvertAll(MapSource);
    }

    public async Task<SapReferenceSourceResponse?> GetSourceAsync(int sourceId, CancellationToken cancellationToken = default)
    {
        var s = await db.SapReferenceSources.AsNoTracking().FirstOrDefaultAsync(x => x.Id == sourceId, cancellationToken);
        return s is null ? null : MapSource(s);
    }

    public async Task<SapReferenceSourceResponse> CreateSourceAsync(CreateSapReferenceSourceRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Name is required.", nameof(request));
        }

        var name = request.Name.Trim();
        if (await db.SapReferenceSources.AnyAsync(
                s => s.Name.ToLower() == name.ToLower(),
                cancellationToken))
        {
            throw new ArgumentException("A source with that name already exists.");
        }

        var now = DateTime.UtcNow;
        var entity = new SapReferenceSource
        {
            Name = name,
            Description = request.Description?.Trim(),
            SourceType = request.SourceType ?? SapReferenceSourceType.Manual,
            SystemLabel = request.SystemLabel?.Trim(),
            Client = request.Client?.Trim(),
            Environment = request.Environment?.Trim(),
            IsEnabled = request.IsEnabled ?? true,
            CreatedAtUtc = now,
        };
        db.SapReferenceSources.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        return MapSource(entity);
    }

    public async Task<SapReferenceSourceResponse?> UpdateSourceAsync(int sourceId, UpdateSapReferenceSourceRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Name is required.", nameof(request));
        }

        var entity = await db.SapReferenceSources.FirstOrDefaultAsync(s => s.Id == sourceId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.Name = request.Name.Trim();
        entity.Description = request.Description?.Trim();
        if (request.SourceType is { } st)
        {
            entity.SourceType = st;
        }

        entity.SystemLabel = request.SystemLabel?.Trim();
        entity.Client = request.Client?.Trim();
        entity.Environment = request.Environment?.Trim();
        if (request.IsEnabled is { } en)
        {
            entity.IsEnabled = en;
        }

        entity.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return MapSource(entity);
    }

    public async Task<SapReferenceSourceResponse?> SetSourceEnabledAsync(int sourceId, bool isEnabled, CancellationToken cancellationToken = default)
    {
        var entity = await db.SapReferenceSources.FirstOrDefaultAsync(s => s.Id == sourceId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.IsEnabled = isEnabled;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return MapSource(entity);
    }

    public async Task<bool> DeleteSourceAsync(int sourceId, CancellationToken cancellationToken = default)
    {
        var entity = await db.SapReferenceSources.FirstOrDefaultAsync(s => s.Id == sourceId, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        db.SapReferenceSources.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<SapTableMetadataResponse>> ListTablesAsync(int sourceId, CancellationToken cancellationToken = default)
    {
        if (!await db.SapReferenceSources.AnyAsync(s => s.Id == sourceId, cancellationToken))
        {
            return [];
        }

        var rows = await db.SapTables.AsNoTracking()
            .Where(t => t.SapReferenceSourceId == sourceId)
            .OrderBy(t => t.TableName)
            .Select(t => new
            {
                t.Id,
                t.SapReferenceSourceId,
                t.TableName,
                t.Description,
                t.Module,
                t.BusinessObject,
                t.DataDomain,
                t.IsCustom,
                t.Notes,
                t.CreatedAtUtc,
                t.UpdatedAtUtc,
                FieldCount = t.Fields.Count,
            })
            .ToListAsync(cancellationToken);

        return rows.ConvertAll(t => new SapTableMetadataResponse(
            t.Id,
            t.SapReferenceSourceId,
            t.TableName,
            t.Description,
            t.Module,
            t.BusinessObject,
            t.DataDomain,
            t.IsCustom,
            t.Notes,
            t.CreatedAtUtc,
            t.UpdatedAtUtc,
            t.FieldCount));
    }

    public async Task<SapTableMetadataResponse?> GetTableAsync(int tableId, CancellationToken cancellationToken = default)
    {
        var t = await db.SapTables.AsNoTracking()
            .Where(x => x.Id == tableId)
            .Select(x => new
            {
                x.Id,
                x.SapReferenceSourceId,
                x.TableName,
                x.Description,
                x.Module,
                x.BusinessObject,
                x.DataDomain,
                x.IsCustom,
                x.Notes,
                x.CreatedAtUtc,
                x.UpdatedAtUtc,
                FieldCount = x.Fields.Count,
            })
            .FirstOrDefaultAsync(cancellationToken);

        return t is null
            ? null
            : new SapTableMetadataResponse(
                t.Id,
                t.SapReferenceSourceId,
                t.TableName,
                t.Description,
                t.Module,
                t.BusinessObject,
                t.DataDomain,
                t.IsCustom,
                t.Notes,
                t.CreatedAtUtc,
                t.UpdatedAtUtc,
                t.FieldCount);
    }

    public async Task<SapTableMetadataResponse?> CreateTableAsync(int sourceId, CreateSapTableMetadataRequest request, CancellationToken cancellationToken = default)
    {
        if (!await db.SapReferenceSources.AnyAsync(s => s.Id == sourceId, cancellationToken))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(request.TableName))
        {
            throw new ArgumentException("TableName is required.", nameof(request));
        }

        var name = NormalizeSapName(request.TableName);
        if (await db.SapTables.AnyAsync(t => t.SapReferenceSourceId == sourceId && t.TableName == name, cancellationToken))
        {
            throw new ArgumentException($"Table '{name}' already exists in this reference source.");
        }

        var now = DateTime.UtcNow;
        var isCustom = request.IsCustom ?? InferTableIsCustom(name);
        var entity = new SapTableMetadata
        {
            SapReferenceSourceId = sourceId,
            TableName = name,
            Description = request.Description?.Trim(),
            Module = NormalizeOptionalUpper(request.Module, uppercase: true),
            BusinessObject = request.BusinessObject?.Trim(),
            DataDomain = request.DataDomain?.Trim(),
            IsCustom = isCustom,
            Notes = request.Notes?.Trim(),
            CreatedAtUtc = now,
        };
        db.SapTables.Add(entity);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ArgumentException($"Table '{name}' already exists in this reference source.");
        }

        return await GetTableAsync(entity.Id, cancellationToken);
    }

    public async Task<SapTableMetadataResponse?> UpdateTableAsync(int tableId, UpdateSapTableMetadataRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.TableName))
        {
            throw new ArgumentException("TableName is required.", nameof(request));
        }

        var entity = await db.SapTables.FirstOrDefaultAsync(t => t.Id == tableId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var name = NormalizeSapName(request.TableName);
        if (await db.SapTables.AnyAsync(
                t => t.SapReferenceSourceId == entity.SapReferenceSourceId && t.TableName == name && t.Id != tableId,
                cancellationToken))
        {
            throw new ArgumentException($"Table '{name}' already exists in this reference source.");
        }

        entity.TableName = name;
        entity.Description = request.Description?.Trim();
        entity.Module = NormalizeOptionalUpper(request.Module, uppercase: true);
        entity.BusinessObject = request.BusinessObject?.Trim();
        entity.DataDomain = request.DataDomain?.Trim();
        entity.IsCustom = request.IsCustom;
        entity.Notes = request.Notes?.Trim();
        entity.UpdatedAtUtc = DateTime.UtcNow;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ArgumentException($"Table '{name}' already exists in this reference source.");
        }

        return await GetTableAsync(tableId, cancellationToken);
    }

    public async Task<bool> DeleteTableAsync(int tableId, CancellationToken cancellationToken = default)
    {
        var entity = await db.SapTables.FirstOrDefaultAsync(t => t.Id == tableId, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        db.SapTables.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<SapFieldMetadataResponse>> ListFieldsAsync(int tableId, CancellationToken cancellationToken = default)
    {
        if (!await db.SapTables.AnyAsync(t => t.Id == tableId, cancellationToken))
        {
            return [];
        }

        var rows = await db.SapFields.AsNoTracking()
            .Where(f => f.SapTableMetadataId == tableId)
            .OrderBy(f => f.FieldName)
            .ToListAsync(cancellationToken);
        return rows.ConvertAll(MapField);
    }

    public async Task<SapFieldMetadataResponse?> CreateFieldAsync(int tableId, CreateSapFieldMetadataRequest request, CancellationToken cancellationToken = default)
    {
        if (!await db.SapTables.AnyAsync(t => t.Id == tableId, cancellationToken))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(request.FieldName))
        {
            throw new ArgumentException("FieldName is required.", nameof(request));
        }

        var fieldName = NormalizeSapName(request.FieldName);
        if (await db.SapFields.AnyAsync(f => f.SapTableMetadataId == tableId && f.FieldName == fieldName, cancellationToken))
        {
            throw new ArgumentException($"Field '{fieldName}' already exists on this table.");
        }

        var isCustom = request.IsCustom ?? InferFieldIsCustom(fieldName);
        var now = DateTime.UtcNow;
        var entity = new SapFieldMetadata
        {
            SapTableMetadataId = tableId,
            FieldName = fieldName,
            Description = request.Description?.Trim(),
            DataElement = NormalizeOptionalUpper(request.DataElement, uppercase: true),
            DomainName = NormalizeOptionalUpper(request.DomainName, uppercase: true),
            DataType = request.DataType?.Trim(),
            Length = request.Length,
            IsKey = request.IsKey ?? false,
            IsRequired = request.IsRequired,
            IsCustom = isCustom,
            BusinessMeaning = request.BusinessMeaning?.Trim(),
            ExampleValue = request.ExampleValue?.Trim(),
            Notes = request.Notes?.Trim(),
            CreatedAtUtc = now,
        };
        db.SapFields.Add(entity);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ArgumentException($"Field '{fieldName}' already exists on this table.");
        }

        return MapField(entity);
    }

    public async Task<SapFieldMetadataResponse?> UpdateFieldAsync(int fieldId, UpdateSapFieldMetadataRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.FieldName))
        {
            throw new ArgumentException("FieldName is required.", nameof(request));
        }

        var entity = await db.SapFields.FirstOrDefaultAsync(f => f.Id == fieldId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var fieldName = NormalizeSapName(request.FieldName);
        if (await db.SapFields.AnyAsync(
                f => f.SapTableMetadataId == entity.SapTableMetadataId && f.FieldName == fieldName && f.Id != fieldId,
                cancellationToken))
        {
            throw new ArgumentException($"Field '{fieldName}' already exists on this table.");
        }

        entity.FieldName = fieldName;
        entity.Description = request.Description?.Trim();
        entity.DataElement = NormalizeOptionalUpper(request.DataElement, uppercase: true);
        entity.DomainName = NormalizeOptionalUpper(request.DomainName, uppercase: true);
        entity.DataType = request.DataType?.Trim();
        entity.Length = request.Length;
        entity.IsKey = request.IsKey;
        entity.IsRequired = request.IsRequired;
        entity.IsCustom = request.IsCustom;
        entity.BusinessMeaning = request.BusinessMeaning?.Trim();
        entity.ExampleValue = request.ExampleValue?.Trim();
        entity.Notes = request.Notes?.Trim();
        entity.UpdatedAtUtc = DateTime.UtcNow;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ArgumentException($"Field '{fieldName}' already exists on this table.");
        }

        return MapField(entity);
    }

    public async Task<bool> DeleteFieldAsync(int fieldId, CancellationToken cancellationToken = default)
    {
        var entity = await db.SapFields.FirstOrDefaultAsync(f => f.Id == fieldId, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        db.SapFields.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<SapDomainValueResponse>> ListDomainValuesAsync(int sourceId, CancellationToken cancellationToken = default)
    {
        if (!await db.SapReferenceSources.AnyAsync(s => s.Id == sourceId, cancellationToken))
        {
            return [];
        }

        var rows = await db.SapDomainValues.AsNoTracking()
            .Where(d => d.SapReferenceSourceId == sourceId)
            .OrderBy(d => d.DomainName)
            .ThenBy(d => d.Value)
            .ToListAsync(cancellationToken);
        return rows.ConvertAll(MapDomain);
    }

    public async Task<SapDomainValueResponse?> CreateDomainValueAsync(int sourceId, CreateSapDomainValueRequest request, CancellationToken cancellationToken = default)
    {
        if (!await db.SapReferenceSources.AnyAsync(s => s.Id == sourceId, cancellationToken))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(request.DomainName) || string.IsNullOrWhiteSpace(request.Value))
        {
            throw new ArgumentException("DomainName and Value are required.", nameof(request));
        }

        var domain = NormalizeSapName(request.DomainName);
        var value = request.Value.Trim();
        var now = DateTime.UtcNow;
        var entity = new SapDomainValueMetadata
        {
            SapReferenceSourceId = sourceId,
            DomainName = domain,
            Value = value.Length > 60 ? value[..60] : value,
            Description = request.Description?.Trim(),
            Notes = request.Notes?.Trim(),
            CreatedAtUtc = now,
        };
        db.SapDomainValues.Add(entity);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ArgumentException("A domain value with this domain and value already exists for this source.");
        }

        return MapDomain(entity);
    }

    public async Task<SapDomainValueResponse?> UpdateDomainValueAsync(int domainValueId, UpdateSapDomainValueRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DomainName) || string.IsNullOrWhiteSpace(request.Value))
        {
            throw new ArgumentException("DomainName and Value are required.", nameof(request));
        }

        var entity = await db.SapDomainValues.FirstOrDefaultAsync(d => d.Id == domainValueId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        var domain = NormalizeSapName(request.DomainName);
        var value = request.Value.Trim();
        if (value.Length > 60)
        {
            value = value[..60];
        }

        entity.DomainName = domain;
        entity.Value = value;
        entity.Description = request.Description?.Trim();
        entity.Notes = request.Notes?.Trim();
        entity.UpdatedAtUtc = DateTime.UtcNow;
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw new ArgumentException("A domain value with this domain and value already exists for this source.");
        }

        return MapDomain(entity);
    }

    public async Task<bool> DeleteDomainValueAsync(int id, CancellationToken cancellationToken = default)
    {
        var entity = await db.SapDomainValues.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (entity is null)
        {
            return false;
        }

        db.SapDomainValues.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<SapReferenceSearchResultDto>> SearchAsync(string query, CancellationToken cancellationToken = default)
    {
        var qRaw = query.Trim();
        if (string.IsNullOrEmpty(qRaw))
        {
            return [];
        }

        var q = qRaw.ToUpperInvariant();
        var results = new List<SapReferenceSearchResultDto>();

        var tables = await db.SapTables.AsNoTracking()
            .Include(t => t.SapReferenceSource)
            .Where(t => t.SapReferenceSource.IsEnabled &&
                        (t.TableName.Contains(q) ||
                         (t.Description != null && t.Description.ToUpper().Contains(q)) ||
                         (t.Module != null && t.Module.ToUpper().Contains(q)) ||
                         (t.BusinessObject != null && t.BusinessObject.ToUpper().Contains(q)) ||
                         (t.DataDomain != null && t.DataDomain.ToUpper().Contains(q)) ||
                         (t.Notes != null && t.Notes.ToUpper().Contains(q))))
            .Take(40)
            .ToListAsync(cancellationToken);

        foreach (var t in tables)
        {
            var src = t.SapReferenceSource;
            var reason = ResolveTableReason(t, q);
            results.Add(new SapReferenceSearchResultDto(
                "Table",
                src.Id,
                src.Name,
                t.Id,
                t.TableName,
                null,
                null,
                t.TableName,
                t.BusinessObject ?? t.Module,
                t.Description,
                t.IsCustom,
                t.Module,
                t.BusinessObject,
                reason,
                null));
        }

        var fields = await db.SapFields.AsNoTracking()
            .Include(f => f.SapTableMetadata)
                .ThenInclude(tab => tab.SapReferenceSource)
            .Where(f => f.SapTableMetadata.SapReferenceSource.IsEnabled &&
                        (f.FieldName.Contains(q) ||
                         (f.Description != null && f.Description.ToUpper().Contains(q)) ||
                         (f.DataElement != null && f.DataElement.ToUpper().Contains(q)) ||
                         (f.DomainName != null && f.DomainName.ToUpper().Contains(q)) ||
                         (f.BusinessMeaning != null && f.BusinessMeaning.ToUpper().Contains(q)) ||
                         (f.Notes != null && f.Notes.ToUpper().Contains(q)) ||
                         f.SapTableMetadata.TableName.Contains(q) ||
                         (f.SapTableMetadata.Description != null && f.SapTableMetadata.Description.ToUpper().Contains(q)) ||
                         (f.SapTableMetadata.BusinessObject != null &&
                          f.SapTableMetadata.BusinessObject.ToUpper().Contains(q)) ||
                         (f.SapTableMetadata.Module != null && f.SapTableMetadata.Module.ToUpper().Contains(q))))
            .Take(50)
            .ToListAsync(cancellationToken);

        foreach (var f in fields)
        {
            var tab = f.SapTableMetadata;
            var src = tab.SapReferenceSource;
            var reason = ResolveFieldReason(f, tab, q);
            results.Add(new SapReferenceSearchResultDto(
                "Field",
                src.Id,
                src.Name,
                tab.Id,
                tab.TableName,
                f.Id,
                f.FieldName,
                f.FieldName,
                $"Field on {tab.TableName}",
                f.Description ?? f.BusinessMeaning,
                f.IsCustom,
                tab.Module,
                tab.BusinessObject,
                reason,
                null));
        }

        var domains = await db.SapDomainValues.AsNoTracking()
            .Include(d => d.SapReferenceSource)
            .Where(d => d.SapReferenceSource.IsEnabled &&
                        (d.DomainName.Contains(q) ||
                         d.Value.ToUpper().Contains(q) ||
                         (d.Description != null && d.Description.ToUpper().Contains(q)) ||
                         (d.Notes != null && d.Notes.ToUpper().Contains(q))))
            .Take(30)
            .ToListAsync(cancellationToken);

        foreach (var d in domains)
        {
            var src = d.SapReferenceSource;
            results.Add(new SapReferenceSearchResultDto(
                "DomainValue",
                src.Id,
                src.Name,
                null,
                null,
                null,
                null,
                $"{d.DomainName}: {d.Value}",
                "Domain value",
                d.Description,
                null,
                null,
                null,
                "Matched domain name or value",
                d.Id));
        }

        return results
            .OrderBy(r => r.ResultType switch { "Table" => 0, "Field" => 1, _ => 2 })
            .ThenBy(r => r.Title)
            .Take(SearchMaxResults)
            .ToList();
    }

    private static string ResolveTableReason(SapTableMetadata t, string qUpper)
    {
        if (t.TableName.Contains(qUpper, StringComparison.Ordinal))
        {
            return "Matched table name";
        }

        if (t.BusinessObject?.ToUpperInvariant().Contains(qUpper, StringComparison.Ordinal) == true)
        {
            return "Matched business object";
        }

        if (t.Description?.ToUpperInvariant().Contains(qUpper, StringComparison.Ordinal) == true)
        {
            return "Matched description";
        }

        return "Matched module, data domain, or notes";
    }

    private static string ResolveFieldReason(SapFieldMetadata f, SapTableMetadata tab, string qUpper)
    {
        if (f.FieldName.Contains(qUpper, StringComparison.Ordinal))
        {
            return "Matched field name";
        }

        if (f.DataElement?.ToUpperInvariant().Contains(qUpper, StringComparison.Ordinal) == true)
        {
            return "Matched data element";
        }

        if (f.DomainName?.ToUpperInvariant().Contains(qUpper, StringComparison.Ordinal) == true)
        {
            return "Matched domain";
        }

        if (tab.BusinessObject?.ToUpperInvariant().Contains(qUpper, StringComparison.Ordinal) == true)
        {
            return "Matched table business object";
        }

        if (f.BusinessMeaning?.ToUpperInvariant().Contains(qUpper, StringComparison.Ordinal) == true)
        {
            return "Matched business meaning";
        }

        return "Matched description, notes, or table metadata";
    }

    private static bool InferFieldIsCustom(string normalizedFieldName) =>
        normalizedFieldName.Length >= 2 &&
        (normalizedFieldName.StartsWith("YY", StringComparison.Ordinal) ||
         normalizedFieldName.StartsWith("ZZ", StringComparison.Ordinal));

    private static bool InferTableIsCustom(string normalizedTableName) =>
        normalizedTableName.Length >= 2 &&
        (normalizedTableName.StartsWith("YY", StringComparison.Ordinal) ||
         normalizedTableName.StartsWith("ZZ", StringComparison.Ordinal));

    private static string NormalizeSapName(string value) => value.Trim().ToUpperInvariant();

    private static string? NormalizeOptionalUpper(string? value, bool uppercase)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var t = value.Trim();
        return uppercase ? t.ToUpperInvariant() : t;
    }

    private static SapReferenceSourceResponse MapSource(SapReferenceSource s) =>
        new(
            s.Id,
            s.Name,
            s.Description,
            s.SourceType,
            s.SystemLabel,
            s.Client,
            s.Environment,
            s.IsEnabled,
            s.CreatedAtUtc,
            s.UpdatedAtUtc);

    private static SapFieldMetadataResponse MapField(SapFieldMetadata f) =>
        new(
            f.Id,
            f.SapTableMetadataId,
            f.FieldName,
            f.Description,
            f.DataElement,
            f.DomainName,
            f.DataType,
            f.Length,
            f.IsKey,
            f.IsRequired,
            f.IsCustom,
            f.BusinessMeaning,
            f.ExampleValue,
            f.Notes,
            f.CreatedAtUtc,
            f.UpdatedAtUtc);

    private static SapDomainValueResponse MapDomain(SapDomainValueMetadata d) =>
        new(
            d.Id,
            d.SapReferenceSourceId,
            d.DomainName,
            d.Value,
            d.Description,
            d.Notes,
            d.CreatedAtUtc,
            d.UpdatedAtUtc);
}
