using System.Text.Json;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public sealed class IntegrationActivityService(CortexDbContext db) : IIntegrationActivityService
{
    private const int MaxTextLength = 2000;

    private const int MaxMetadataJsonLength = 2000;

    public async Task RecordAsync(IntegrationActivityLogRecordRequest request, CancellationToken cancellationToken = default)
    {
        if (!await db.ExternalWorkSources.AsNoTracking().AnyAsync(s => s.Id == request.ExternalWorkSourceId, cancellationToken))
        {
            return;
        }

        var connectionId = request.IntegrationConnectionId
            ?? await db.ExternalWorkSources.AsNoTracking()
                .Where(s => s.Id == request.ExternalWorkSourceId)
                .Select(s => (int?)s.IntegrationConnectionId)
                .FirstAsync(cancellationToken);

        var completed = request.CompletedAtUtc;
        var started = request.StartedAtUtc;
        var durationMs = completed >= started
            ? (long?)Math.Round((completed - started).TotalMilliseconds)
            : null;

        var entity = new IntegrationActivityLog
        {
            IntegrationConnectionId = connectionId,
            ExternalWorkSourceId = request.ExternalWorkSourceId,
            ActivityType = request.ActivityType,
            Status = request.Status,
            TriggeredByUserId = request.TriggeredByUserId,
            TriggeredByDisplayName = TrimSafe(request.TriggeredByDisplayName, 200),
            TriggeredByEmail = TrimSafe(request.TriggeredByEmail, 200),
            StartedAtUtc = started,
            CompletedAtUtc = completed,
            DurationMs = durationMs,
            CreatedCount = request.CreatedCount,
            UpdatedCount = request.UpdatedCount,
            UnchangedCount = request.UnchangedCount,
            SkippedCount = request.SkippedCount,
            ErrorCount = request.ErrorCount,
            ItemCount = request.ItemCount,
            Message = TrimSafe(request.Message, MaxTextLength),
            ErrorMessage = TrimSafe(request.ErrorMessage, MaxTextLength),
            MetadataJson = TrimSafe(request.MetadataJson, MaxMetadataJsonLength),
        };

        db.IntegrationActivityLogs.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<IntegrationActivityLogResponse>?> GetSourceActivityAsync(
        int sourceId,
        int take = 20,
        string? activityType = null,
        CancellationToken cancellationToken = default)
    {
        if (!await db.ExternalWorkSources.AsNoTracking().AnyAsync(s => s.Id == sourceId, cancellationToken))
        {
            return null;
        }

        var limit = Math.Clamp(take, 1, 100);
        var query = db.IntegrationActivityLogs.AsNoTracking()
            .Where(a => a.ExternalWorkSourceId == sourceId);

        if (!string.IsNullOrWhiteSpace(activityType)
            && Enum.TryParse<IntegrationActivityType>(activityType.Trim(), ignoreCase: true, out var parsed))
        {
            query = query.Where(a => a.ActivityType == parsed);
        }

        var rows = await query
            .OrderByDescending(a => a.StartedAtUtc)
            .ThenByDescending(a => a.Id)
            .Take(limit)
            .Select(a => new IntegrationActivityLogResponse(
                a.Id,
                a.ExternalWorkSourceId,
                a.IntegrationConnectionId,
                a.ActivityType,
                a.Status,
                a.TriggeredByDisplayName,
                a.StartedAtUtc,
                a.CompletedAtUtc,
                a.DurationMs,
                a.CreatedCount,
                a.UpdatedCount,
                a.UnchangedCount,
                a.SkippedCount,
                a.ErrorCount,
                a.ItemCount,
                a.Message,
                a.ErrorMessage))
            .ToListAsync(cancellationToken);

        return rows;
    }

    private static string? TrimSafe(string? value, int maxLen)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        var t = value.Trim();
        if (t.Length <= maxLen)
        {
            return t;
        }

        return t[..maxLen];
    }

    /// <summary>Compact metadata for discovery row counts only.</summary>
    public static string? BuildFieldCountMetadata(int fieldCount) =>
        JsonSerializer.Serialize(new Dictionary<string, int> { ["fieldCount"] = fieldCount });
}
