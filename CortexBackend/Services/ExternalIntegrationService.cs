using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public sealed class ExternalIntegrationService(CortexDbContext db) : IExternalIntegrationService
{
    private readonly CortexDbContext _db = db;

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
