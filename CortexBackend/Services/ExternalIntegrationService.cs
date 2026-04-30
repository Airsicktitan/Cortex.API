using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services.Integrations;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public sealed class ExternalIntegrationService(
    CortexDbContext db,
    ISharePointGraphClient sharePointGraphClient,
    IEnumerable<IExternalWorkSourceAdapter> workSourceAdapters) : IExternalIntegrationService
{
    private readonly CortexDbContext _db = db;
    private readonly ISharePointGraphClient _graph = sharePointGraphClient;
    private readonly IExternalWorkSourceAdapter _sharePointWorkSourceAdapter = workSourceAdapters
            .FirstOrDefault(a => a.Provider == IntegrationProvider.SharePoint)
        ?? throw new InvalidOperationException(
            "No IExternalWorkSourceAdapter is registered for SharePoint. Register SharePointExternalWorkSourceAdapter.");

    public async Task<IReadOnlyList<IntegrationConnectionResponse>> ListConnectionsAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await (
                from c in _db.IntegrationConnections.AsNoTracking()
                orderby c.DisplayName
                let count = _db.ExternalWorkSources.Count(s => s.IntegrationConnectionId == c.Id)
                select new { Connection = c, SourceCount = count })
            .ToListAsync(cancellationToken);

        return rows.Select(r => MapConnection(r.Connection, r.SourceCount)).ToList();
    }

    public async Task<IntegrationConnectionResponse?> GetConnectionAsync(int id, CancellationToken cancellationToken = default)
    {
        var row = await (
                from c in _db.IntegrationConnections.AsNoTracking()
                where c.Id == id
                let count = _db.ExternalWorkSources.Count(s => s.IntegrationConnectionId == c.Id)
                select new { Connection = c, SourceCount = count })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : MapConnection(row.Connection, row.SourceCount);
    }

    public async Task<IntegrationConnectionResponse> CreateConnectionAsync(
        CreateIntegrationConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ArgumentException("DisplayName is required.", nameof(request));
        }

        var now = DateTime.UtcNow;
        var entity = new IntegrationConnection
        {
            Provider = request.Provider,
            DisplayName = request.DisplayName.Trim(),
            TenantId = request.TenantId?.Trim(),
            OrganizationId = request.OrganizationId?.Trim(),
            AuthMode = request.AuthMode ?? IntegrationAuthMode.Manual,
            SyncMode = request.SyncMode ?? IntegrationSyncMode.ReadOnly,
            IsEnabled = request.IsEnabled ?? true,
            CreatedAtUtc = now,
        };

        _db.IntegrationConnections.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return MapConnection(entity, 0);
    }

    public async Task<IntegrationConnectionResponse?> UpdateConnectionAsync(
        int id,
        UpdateIntegrationConnectionRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.IntegrationConnections.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new ArgumentException("DisplayName is required.", nameof(request));
        }

        entity.DisplayName = request.DisplayName.Trim();
        entity.TenantId = request.TenantId?.Trim();
        entity.OrganizationId = request.OrganizationId?.Trim();
        if (request.AuthMode is { } authMode)
        {
            entity.AuthMode = authMode;
        }

        if (request.SyncMode is { } syncMode)
        {
            entity.SyncMode = syncMode;
        }

        if (request.IsEnabled is { } enabled)
        {
            entity.IsEnabled = enabled;
        }

        entity.LastSyncUtc = request.LastSyncUtc;
        entity.LastSyncStatus = request.LastSyncStatus?.Trim();
        entity.LastSyncMessage = request.LastSyncMessage?.Trim();
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken);

        var count = await _db.ExternalWorkSources.CountAsync(s => s.IntegrationConnectionId == id, cancellationToken);
        return MapConnection(entity, count);
    }

    public async Task<IntegrationConnectionResponse?> SetConnectionEnabledAsync(
        int id,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.IntegrationConnections.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.IsEnabled = isEnabled;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var count = await _db.ExternalWorkSources.CountAsync(s => s.IntegrationConnectionId == id, cancellationToken);
        return MapConnection(entity, count);
    }

    public async Task<IReadOnlyList<ExternalWorkSourceResponse>?> ListSourcesAsync(
        int connectionId,
        CancellationToken cancellationToken = default)
    {
        var connectionExists = await _db.IntegrationConnections.AnyAsync(c => c.Id == connectionId, cancellationToken);
        if (!connectionExists)
        {
            return null;
        }

        var list = await _db.ExternalWorkSources.AsNoTracking()
            .Where(s => s.IntegrationConnectionId == connectionId)
            .OrderBy(s => s.Name)
            .Select(s => new
            {
                s.Id,
                s.IntegrationConnectionId,
                s.Provider,
                s.SourceType,
                s.ExternalSourceId,
                s.Name,
                s.ExternalUrl,
                s.IsEnabled,
                s.CreatedAtUtc,
                s.UpdatedAtUtc,
                FieldMappingCount = s.FieldMappings.Count,
                BoardMappingCount = s.BoardMappings.Count,
            })
            .ToListAsync(cancellationToken);

        return list.ConvertAll(s => new ExternalWorkSourceResponse(
            s.Id,
            s.IntegrationConnectionId,
            s.Provider,
            s.SourceType,
            s.ExternalSourceId,
            s.Name,
            s.ExternalUrl,
            s.IsEnabled,
            s.CreatedAtUtc,
            s.UpdatedAtUtc,
            s.FieldMappingCount,
            s.BoardMappingCount));
    }

    public async Task<ExternalWorkSourceResponse?> CreateSourceAsync(
        int connectionId,
        CreateExternalWorkSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!await _db.IntegrationConnections.AnyAsync(c => c.Id == connectionId, cancellationToken))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(request.ExternalSourceId))
        {
            throw new ArgumentException("ExternalSourceId is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Name is required.", nameof(request));
        }

        var dup = await _db.ExternalWorkSources.AnyAsync(
            s => s.IntegrationConnectionId == connectionId && s.ExternalSourceId == request.ExternalSourceId.Trim(),
            cancellationToken);
        if (dup)
        {
            throw new ArgumentException("A source with this ExternalSourceId already exists for the connection.");
        }

        var now = DateTime.UtcNow;
        var entity = new ExternalWorkSource
        {
            IntegrationConnectionId = connectionId,
            Provider = request.Provider,
            SourceType = request.SourceType,
            ExternalSourceId = request.ExternalSourceId.Trim(),
            Name = request.Name.Trim(),
            ExternalUrl = request.ExternalUrl?.Trim(),
            IsEnabled = request.IsEnabled ?? true,
            CreatedAtUtc = now,
        };

        _db.ExternalWorkSources.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return MapSource(entity, 0, 0);
    }

    public async Task<ExternalWorkSourceResponse?> UpdateSourceAsync(
        int sourceId,
        UpdateExternalWorkSourceRequest request,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.ExternalWorkSources.FirstOrDefaultAsync(s => s.Id == sourceId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(request.Name))
        {
            throw new ArgumentException("Name is required.", nameof(request));
        }

        entity.Name = request.Name.Trim();
        entity.ExternalUrl = request.ExternalUrl?.Trim();
        if (request.Provider is { } p)
        {
            entity.Provider = p;
        }

        if (request.SourceType is { } st)
        {
            entity.SourceType = st;
        }

        if (!string.IsNullOrWhiteSpace(request.ExternalSourceId))
        {
            var newId = request.ExternalSourceId.Trim();
            var duplicate = await _db.ExternalWorkSources.AnyAsync(
                s => s.IntegrationConnectionId == entity.IntegrationConnectionId
                     && s.ExternalSourceId == newId
                     && s.Id != sourceId,
                cancellationToken);
            if (duplicate)
            {
                throw new ArgumentException("A source with this ExternalSourceId already exists for the connection.");
            }

            entity.ExternalSourceId = newId;
        }

        if (request.IsEnabled is { } ie)
        {
            entity.IsEnabled = ie;
        }

        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        var counts = await _db.ExternalWorkSources.AsNoTracking()
            .Where(s => s.Id == sourceId)
            .Select(s => new { Field = s.FieldMappings.Count, Board = s.BoardMappings.Count })
            .FirstAsync(cancellationToken);

        return MapSource(entity, counts.Field, counts.Board);
    }

    public async Task<ExternalWorkSourceResponse?> SetSourceEnabledAsync(
        int sourceId,
        bool isEnabled,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.ExternalWorkSources.FirstOrDefaultAsync(s => s.Id == sourceId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        entity.IsEnabled = isEnabled;
        entity.UpdatedAtUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);
        var counts = await _db.ExternalWorkSources.AsNoTracking()
            .Where(s => s.Id == sourceId)
            .Select(s => new { Field = s.FieldMappings.Count, Board = s.BoardMappings.Count })
            .FirstAsync(cancellationToken);
        return MapSource(entity, counts.Field, counts.Board);
    }

    public async Task<IReadOnlyList<ExternalFieldMappingResponse>?> GetFieldMappingsAsync(
        int sourceId,
        CancellationToken cancellationToken = default)
    {
        if (!await _db.ExternalWorkSources.AnyAsync(s => s.Id == sourceId, cancellationToken))
        {
            return null;
        }

        var maps = await _db.ExternalFieldMappings.AsNoTracking()
            .Where(m => m.ExternalWorkSourceId == sourceId)
            .OrderBy(m => m.ExternalFieldName)
            .ToListAsync(cancellationToken);

        return maps.Select(MapFieldMapping).ToList();
    }

    public async Task<IReadOnlyList<ExternalFieldMappingResponse>?> ReplaceFieldMappingsAsync(
        int sourceId,
        IReadOnlyList<ExternalFieldMappingItemRequest> mappings,
        CancellationToken cancellationToken = default)
    {
        if (!await _db.ExternalWorkSources.AnyAsync(s => s.Id == sourceId, cancellationToken))
        {
            return null;
        }

        ArgumentNullException.ThrowIfNull(mappings);

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in mappings)
        {
            if (string.IsNullOrWhiteSpace(item.ExternalFieldName))
            {
                throw new ArgumentException("Each mapping requires ExternalFieldName.");
            }

            if (!Enum.IsDefined(typeof(CortexField), item.CortexField))
            {
                throw new ArgumentException($"Invalid CortexField value: {(int)item.CortexField}.");
            }

            if (!seen.Add(item.ExternalFieldName.Trim()))
            {
                throw new ArgumentException($"Duplicate external field name: {item.ExternalFieldName}");
            }
        }

        var now = DateTime.UtcNow;
        var existing = await _db.ExternalFieldMappings
            .Where(m => m.ExternalWorkSourceId == sourceId)
            .ToListAsync(cancellationToken);
        _db.ExternalFieldMappings.RemoveRange(existing);

        foreach (var item in mappings)
        {
            _db.ExternalFieldMappings.Add(new ExternalFieldMapping
            {
                ExternalWorkSourceId = sourceId,
                ExternalFieldName = item.ExternalFieldName.Trim(),
                ExternalFieldKey = string.IsNullOrWhiteSpace(item.ExternalFieldKey)
                    ? null
                    : item.ExternalFieldKey.Trim(),
                CortexField = item.CortexField,
                IsRequired = item.IsRequired,
                TransformHint = string.IsNullOrWhiteSpace(item.TransformHint)
                    ? null
                    : item.TransformHint.Trim(),
                CreatedAtUtc = now,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await GetFieldMappingsAsync(sourceId, cancellationToken);
    }

    public async Task<IReadOnlyList<ExternalBoardMappingResponse>?> GetBoardMappingsAsync(
        int sourceId,
        CancellationToken cancellationToken = default)
    {
        if (!await _db.ExternalWorkSources.AnyAsync(s => s.Id == sourceId, cancellationToken))
        {
            return null;
        }

        var maps = await _db.ExternalBoardMappings.AsNoTracking()
            .Where(m => m.ExternalWorkSourceId == sourceId)
            .Join(
                _db.TicketBoardDefinitions.AsNoTracking(),
                m => m.BoardId,
                b => b.Id,
                (m, b) => new { Map = m, b.Name })
            .OrderBy(x => x.Map.BoardId)
            .ToListAsync(cancellationToken);

        return maps.Select(x => MapBoardMapping(x.Map, x.Name)).ToList();
    }

    public async Task<IReadOnlyList<ExternalBoardMappingResponse>?> ReplaceBoardMappingsAsync(
        int sourceId,
        IReadOnlyList<ExternalBoardMappingItemRequest> mappings,
        CancellationToken cancellationToken = default)
    {
        if (!await _db.ExternalWorkSources.AnyAsync(s => s.Id == sourceId, cancellationToken))
        {
            return null;
        }

        ArgumentNullException.ThrowIfNull(mappings);

        foreach (var item in mappings)
        {
            if (!Enum.IsDefined(typeof(ExternalBoardMappingMode), item.MappingMode))
            {
                throw new ArgumentException($"Invalid MappingMode value: {(int)item.MappingMode}.");
            }
        }

        var boardIds = mappings.Select(m => m.BoardId).Distinct().ToList();
        if (boardIds.Count > 0)
        {
            var found = await _db.TicketBoardDefinitions
                .Where(b => boardIds.Contains(b.Id))
                .Select(b => b.Id)
                .ToListAsync(cancellationToken);
            if (found.Count != boardIds.Count)
            {
                throw new ArgumentException("One or more BoardId values are not valid ticket boards.");
            }
        }

        var now = DateTime.UtcNow;
        var existing = await _db.ExternalBoardMappings
            .Where(m => m.ExternalWorkSourceId == sourceId)
            .ToListAsync(cancellationToken);
        _db.ExternalBoardMappings.RemoveRange(existing);

        foreach (var item in mappings)
        {
            _db.ExternalBoardMappings.Add(new ExternalBoardMapping
            {
                ExternalWorkSourceId = sourceId,
                BoardId = item.BoardId,
                MappingMode = item.MappingMode,
                IsDefault = item.IsDefault,
                CreatedAtUtc = now,
            });
        }

        await _db.SaveChangesAsync(cancellationToken);

        return await GetBoardMappingsAsync(sourceId, cancellationToken);
    }

    public async Task<IReadOnlyList<ExternalWorkItemResponse>?> ListWorkItemsAsync(
        int sourceId,
        CancellationToken cancellationToken = default)
    {
        var sourceName = await _db.ExternalWorkSources.AsNoTracking()
            .Where(s => s.Id == sourceId)
            .Select(s => s.Name)
            .FirstOrDefaultAsync(cancellationToken);

        if (sourceName is null)
        {
            return null;
        }

        var items = await _db.ExternalWorkItems.AsNoTracking()
            .Where(i => i.ExternalWorkSourceId == sourceId)
            .OrderByDescending(i => i.LastSeenUtc)
            .ToListAsync(cancellationToken);

        return items.Select(i => MapWorkItem(i, sourceName)).ToList();
    }

    public async Task<ExternalWorkItemResponse?> GetWorkItemAsync(int itemId, CancellationToken cancellationToken = default)
    {
        var row = await (
                from i in _db.ExternalWorkItems.AsNoTracking()
                join s in _db.ExternalWorkSources.AsNoTracking() on i.ExternalWorkSourceId equals s.Id
                where i.Id == itemId
                select new { Item = i, s.Name })
            .FirstOrDefaultAsync(cancellationToken);

        return row is null ? null : MapWorkItem(row.Item, row.Name);
    }

    public async Task<ExternalWorkItemResponse?> ManualUpsertWorkItemAsync(
        int sourceId,
        ManualUpsertExternalWorkItemRequest request,
        CancellationToken cancellationToken = default)
    {
        var source = await _db.ExternalWorkSources.FirstOrDefaultAsync(s => s.Id == sourceId, cancellationToken);
        if (source is null)
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(request.ExternalItemId))
        {
            throw new ArgumentException("ExternalItemId is required.", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException("Title is required.", nameof(request));
        }

        if (!string.IsNullOrWhiteSpace(request.CortexTicketId))
        {
            var ticketExists = await _db.Tickets.AnyAsync(t => t.Id == request.CortexTicketId, cancellationToken);
            if (!ticketExists)
            {
                throw new ArgumentException("CortexTicketId does not match an existing ticket.");
            }
        }

        var now = DateTime.UtcNow;
        var extId = request.ExternalItemId.Trim();
        var existingItem = await _db.ExternalWorkItems
            .FirstOrDefaultAsync(
                i => i.ExternalWorkSourceId == sourceId && i.ExternalItemId == extId,
                cancellationToken);

        if (existingItem is null)
        {
            existingItem = new ExternalWorkItem
            {
                ExternalWorkSourceId = sourceId,
                Provider = source.Provider,
                ExternalItemId = extId,
                CreatedAtUtc = now,
                LastSeenUtc = now,
            };
            _db.ExternalWorkItems.Add(existingItem);
        }
        else
        {
            existingItem.LastSeenUtc = now;
            existingItem.UpdatedAtUtc = now;
        }

        existingItem.ExternalUrl = request.ExternalUrl?.Trim();
        existingItem.Title = request.Title.Trim();
        existingItem.Description = request.Description?.Trim();
        existingItem.Status = request.Status?.Trim();
        existingItem.Priority = request.Priority?.Trim();
        existingItem.Requester = request.Requester?.Trim();
        existingItem.AssignedTo = request.AssignedTo?.Trim();
        existingItem.Department = request.Department?.Trim();
        existingItem.Category = request.Category?.Trim();
        existingItem.DueDateUtc = request.DueDateUtc;
        existingItem.LastModifiedUtc = request.LastModifiedUtc ?? now;
        existingItem.RawJson = string.IsNullOrWhiteSpace(request.RawJson) ? "{}" : request.RawJson!;
        existingItem.SyncHash = request.SyncHash?.Trim();
        existingItem.CortexTicketId = string.IsNullOrWhiteSpace(request.CortexTicketId) ? null : request.CortexTicketId.Trim();
        existingItem.Provider = source.Provider;

        await _db.SaveChangesAsync(cancellationToken);

        return MapWorkItem(existingItem, source.Name);
    }

    public async Task<IReadOnlyList<SharePointDiscoveredFieldResponse>?> DiscoverSharePointFieldsAsync(
        int sourceId,
        CancellationToken cancellationToken = default)
    {
        var source = await _db.ExternalWorkSources.AsNoTracking()
            .Include(s => s.IntegrationConnection)
            .FirstOrDefaultAsync(s => s.Id == sourceId, cancellationToken);
        if (source is null)
        {
            return null;
        }

        if (source.Provider != IntegrationProvider.SharePoint || source.SourceType != ExternalSourceType.SharePointList)
        {
            throw new IntegrationApiException(400, "Field discovery supports SharePoint list sources only.");
        }

        var discovered = await _sharePointWorkSourceAdapter.DiscoverFieldsAsync(source, cancellationToken);
        return discovered.Select(d => new SharePointDiscoveredFieldResponse(
            d.FieldName,
            d.FieldKey,
            d.DisplayName,
            d.TypeHint,
            d.IsHidden,
            d.IsReadOnly,
            d.SuggestedCortexField)).ToList();
    }

    public async Task<ExternalSourceSyncResponse?> SyncSharePointSourceAsync(int sourceId, CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;
        var source = await _db.ExternalWorkSources
            .Include(s => s.IntegrationConnection)
            .Include(s => s.FieldMappings)
            .FirstOrDefaultAsync(s => s.Id == sourceId, cancellationToken);

        if (source is null)
        {
            return null;
        }

        if (source.Provider != IntegrationProvider.SharePoint || source.SourceType != ExternalSourceType.SharePointList)
        {
            throw new IntegrationApiException(400, "Sync supports SharePoint list sources only.");
        }

        if (!source.IsEnabled)
        {
            throw new IntegrationApiException(400, "External work source is disabled.");
        }

        var connection = source.IntegrationConnection;
        if (!connection.IsEnabled)
        {
            throw new IntegrationApiException(400, "Integration connection is disabled.");
        }

        if (!SharePointSiteUrlParser.TryParseListPageUrl(source.ExternalUrl, out var hostname, out var sitePath, out var parseErr))
        {
            throw new IntegrationApiException(400, parseErr ?? "Invalid SharePoint ExternalUrl.");
        }

        if (string.IsNullOrWhiteSpace(source.ExternalSourceId))
        {
            throw new IntegrationApiException(400, "ExternalSourceId (SharePoint list id) is required.");
        }

        if (source.FieldMappings.Count == 0)
        {
            throw new IntegrationApiException(400, "Configure at least one field mapping before syncing.");
        }

        var tenant = connection.TenantId;
        IReadOnlyList<System.Text.Json.JsonElement> items;
        try
        {
            var site = await _graph.GetSiteByPathAsync(hostname, sitePath, tenant, cancellationToken);
            items = await _graph.GetListItemsAsync(site.Id, source.ExternalSourceId, tenant, cancellationToken);
        }
        catch (IntegrationApiException ex)
        {
            ApplyConnectionSyncFields(connection, DateTime.UtcNow, "Failed", ex.Message);
            await _db.SaveChangesAsync(cancellationToken);
            throw;
        }
        catch (Exception)
        {
            ApplyConnectionSyncFields(connection, DateTime.UtcNow, "Failed", "Unexpected error calling Microsoft Graph.");
            await _db.SaveChangesAsync(cancellationToken);
            throw new IntegrationApiException(502, "SharePoint sync failed due to an unexpected error.");
        }

        var created = 0;
        var updated = 0;
        var unchanged = 0;
        var skipped = 0;
        var errors = 0;
        var mappings = source.FieldMappings.ToList();

        foreach (var item in items)
        {
            try
            {
                if (!SharePointListItemNormalizer.TryNormalize(item, mappings, source.ExternalUrl, out var norm, out _))
                {
                    skipped++;
                    continue;
                }

                ArgumentNullException.ThrowIfNull(norm);

                var now = DateTime.UtcNow;
                var existingItem = await _db.ExternalWorkItems.FirstOrDefaultAsync(
                    i => i.ExternalWorkSourceId == source.Id && i.ExternalItemId == norm.ExternalItemId,
                    cancellationToken);

                if (existingItem is null)
                {
                    existingItem = new ExternalWorkItem
                    {
                        ExternalWorkSourceId = source.Id,
                        Provider = source.Provider,
                        ExternalItemId = norm.ExternalItemId,
                        CreatedAtUtc = now,
                        LastSeenUtc = now,
                    };
                    _db.ExternalWorkItems.Add(existingItem);
                    ApplySharePointNormalized(existingItem, norm, now, newItem: true);
                    created++;
                }
                else if (IsSharePointNormalizedUnchanged(existingItem, norm))
                {
                    existingItem.LastSeenUtc = now;
                    existingItem.UpdatedAtUtc = now;
                    unchanged++;
                }
                else
                {
                    ApplySharePointNormalized(existingItem, norm, now, newItem: false);
                    updated++;
                }
            }
            catch
            {
                errors++;
            }
        }

        var completed = DateTime.UtcNow;
        var status = errors > 0 && created + updated + unchanged == 0
            ? "Failed"
            : errors > 0
                ? "Partial"
                : "Success";
        var message = errors > 0
            ? $"Synced with {errors} row error(s). Created {created}, updated {updated}, unchanged {unchanged}, skipped {skipped}."
            : $"Created {created}, updated {updated}, unchanged {unchanged}, skipped {skipped}.";

        ApplyConnectionSyncFields(connection, completed, status, message);
        await _db.SaveChangesAsync(cancellationToken);

        return new ExternalSourceSyncResponse(
            source.Id,
            source.Name,
            source.Provider,
            started,
            completed,
            created,
            updated,
            unchanged,
            skipped,
            errors,
            items.Count,
            message);
    }

    private static void ApplyConnectionSyncFields(IntegrationConnection connection, DateTime syncUtc, string status, string message)
    {
        connection.LastSyncUtc = syncUtc;
        connection.LastSyncStatus = status;
        connection.LastSyncMessage = message.Length > 2000 ? message[..2000] : message;
        connection.UpdatedAtUtc = DateTime.UtcNow;
    }

    private static bool IsSharePointNormalizedUnchanged(ExternalWorkItem row, SharePointListItemNormalizer.NormalizedRow norm)
    {
        return string.Equals(row.Title, norm.Title, StringComparison.Ordinal)
            && string.Equals(row.Description ?? "", norm.Description ?? "", StringComparison.Ordinal)
            && string.Equals(row.Status ?? "", norm.Status ?? "", StringComparison.Ordinal)
            && string.Equals(row.Priority ?? "", norm.Priority ?? "", StringComparison.Ordinal)
            && string.Equals(row.Requester ?? "", norm.Requester ?? "", StringComparison.Ordinal)
            && string.Equals(row.AssignedTo ?? "", norm.AssignedTo ?? "", StringComparison.Ordinal)
            && string.Equals(row.Department ?? "", norm.Department ?? "", StringComparison.Ordinal)
            && string.Equals(row.Category ?? "", norm.Category ?? "", StringComparison.Ordinal)
            && row.DueDateUtc == norm.DueDateUtc
            && string.Equals(row.ExternalUrl ?? "", norm.ExternalUrl ?? "", StringComparison.Ordinal)
            && string.Equals(row.RawJson, norm.RawJson, StringComparison.Ordinal);
    }

    private static void ApplySharePointNormalized(
        ExternalWorkItem row,
        SharePointListItemNormalizer.NormalizedRow norm,
        DateTime now,
        bool newItem)
    {
        var preserveTicket = row.CortexTicketId;
        row.Title = norm.Title;
        row.Description = norm.Description;
        row.Status = norm.Status;
        row.Priority = norm.Priority;
        row.Requester = norm.Requester;
        row.AssignedTo = norm.AssignedTo;
        row.Department = norm.Department;
        row.Category = norm.Category;
        row.DueDateUtc = norm.DueDateUtc;
        row.LastModifiedUtc = norm.LastModifiedUtc ?? now;
        row.LastSeenUtc = now;
        row.ExternalUrl = norm.ExternalUrl;
        row.RawJson = norm.RawJson;
        row.Provider = IntegrationProvider.SharePoint;
        row.UpdatedAtUtc = now;
        if (!newItem)
        {
            row.CortexTicketId = preserveTicket;
        }
    }

    private static IntegrationConnectionResponse MapConnection(IntegrationConnection c, int sourceCount) =>
        new(
            c.Id,
            c.Provider,
            c.DisplayName,
            c.TenantId,
            c.OrganizationId,
            c.AuthMode,
            c.SyncMode,
            c.IsEnabled,
            c.LastSyncUtc,
            c.LastSyncStatus,
            c.LastSyncMessage,
            c.CreatedAtUtc,
            c.UpdatedAtUtc,
            sourceCount);

    private static ExternalWorkSourceResponse MapSource(
        ExternalWorkSource s,
        int fieldCount,
        int boardCount) =>
        new(
            s.Id,
            s.IntegrationConnectionId,
            s.Provider,
            s.SourceType,
            s.ExternalSourceId,
            s.Name,
            s.ExternalUrl,
            s.IsEnabled,
            s.CreatedAtUtc,
            s.UpdatedAtUtc,
            fieldCount,
            boardCount);

    private static ExternalFieldMappingResponse MapFieldMapping(ExternalFieldMapping m) =>
        new(
            m.Id,
            m.ExternalFieldName,
            m.ExternalFieldKey,
            m.CortexField,
            m.IsRequired,
            m.TransformHint,
            m.CreatedAtUtc,
            m.UpdatedAtUtc);

    private static ExternalBoardMappingResponse MapBoardMapping(ExternalBoardMapping m, string boardName) =>
        new(
            m.Id,
            m.BoardId,
            boardName,
            m.MappingMode,
            m.IsDefault,
            m.CreatedAtUtc,
            m.UpdatedAtUtc);

    private static ExternalWorkItemResponse MapWorkItem(ExternalWorkItem i, string sourceName) =>
        new(
            i.Id,
            i.Provider,
            sourceName,
            i.ExternalItemId,
            i.ExternalUrl,
            i.Title,
            i.Description,
            i.Status,
            i.Priority,
            i.Requester,
            i.AssignedTo,
            i.Department,
            i.Category,
            i.DueDateUtc,
            i.LastModifiedUtc,
            i.LastSeenUtc,
            i.IsDeleted,
            i.CortexTicketId);
}
