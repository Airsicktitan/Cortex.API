using System.Text.Json;
using Cortex.API.Database;
using Cortex.API.DTO;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Handlers;

/// <summary>Read-only aggregated metrics from WorkflowMetricEvents.</summary>
public static class MetricsHandlers
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>GET /api/metrics/snapshot</summary>
    public static async Task<IResult> GetWorkflowMetricsSnapshot(
        CortexDbContext db,
        CancellationToken cancellationToken)
    {
        var snapshot = await BuildSnapshotAsync(db, cancellationToken);
        return Results.Ok(snapshot);
    }

    internal static async Task<WorkflowMetricsSnapshotResponse> BuildSnapshotAsync(
        CortexDbContext db,
        CancellationToken cancellationToken)
    {
        const string intakeRequested = "intake_assist_requested";
        const string intakeSaved = "intake_assist_saved";
        const string intakeCompleted = "intake_assist_completed";
        const string reviewerShown = "reviewer_quality_signal_shown";
        const string screenshotRequested = "screenshot_insight_requested";

        var intakeUsage = await db.WorkflowMetricEvents
            .AsNoTracking()
            .CountAsync(e => e.EventType == intakeRequested, cancellationToken);

        var intakeSavedCount = await db.WorkflowMetricEvents
            .AsNoTracking()
            .CountAsync(e => e.EventType == intakeSaved, cancellationToken);

        var screenshotUsage = await db.WorkflowMetricEvents
            .AsNoTracking()
            .CountAsync(e => e.EventType == screenshotRequested, cancellationToken);

        var completedPayloads = await db.WorkflowMetricEvents
            .AsNoTracking()
            .Where(e => e.EventType == intakeCompleted)
            .Select(e => e.PayloadJson)
            .ToListAsync(cancellationToken);

        var missingDetailValues = new List<int>();
        foreach (var json in completedPayloads)
        {
            if (TryGetInt32Property(json, "missingDetailCount", out var n) && n >= 0)
            {
                missingDetailValues.Add(n);
            }
        }

        var avgMissing = missingDetailValues.Count > 0
            ? missingDetailValues.Average()
            : 0d;

        var reviewerPayloads = await db.WorkflowMetricEvents
            .AsNoTracking()
            .Where(e => e.EventType == reviewerShown)
            .Select(e => e.PayloadJson)
            .ToListAsync(cancellationToken);

        var readyCount = 0;
        var gapsCount = 0;
        var needsDetailCount = 0;

        var commentSumReady = 0.0;
        var commentCountReady = 0;
        var commentSumGaps = 0.0;
        var commentCountGaps = 0;
        var commentSumNeeds = 0.0;
        var commentCountNeeds = 0;

        foreach (var json in reviewerPayloads)
        {
            if (!TryGetStringProperty(json, "reviewerSignal", out var signalRaw))
            {
                continue;
            }

            var signal = signalRaw.Trim().ToLowerInvariant();
            switch (signal)
            {
                case "ready":
                    readyCount++;
                    if (TryGetInt32Property(json, "commentCount", out var c0) && c0 >= 0)
                    {
                        commentSumReady += c0;
                        commentCountReady++;
                    }

                    break;
                case "gaps":
                    gapsCount++;
                    if (TryGetInt32Property(json, "commentCount", out var c1) && c1 >= 0)
                    {
                        commentSumGaps += c1;
                        commentCountGaps++;
                    }

                    break;
                case "needs_detail":
                    needsDetailCount++;
                    if (TryGetInt32Property(json, "commentCount", out var c2) && c2 >= 0)
                    {
                        commentSumNeeds += c2;
                        commentCountNeeds++;
                    }

                    break;
            }
        }

        return new WorkflowMetricsSnapshotResponse
        {
            IntakeAssistUsageCount = intakeUsage,
            IntakeAssistSavedCount = intakeSavedCount,
            AvgMissingDetailCount = avgMissing,
            ReviewerSignalCounts = new ReviewerSignalCountsDto
            {
                Ready = readyCount,
                Gaps = gapsCount,
                NeedsDetail = needsDetailCount,
            },
            ScreenshotInsightUsageCount = screenshotUsage,
            AvgCommentCountBySignal = new AvgCommentCountBySignalDto
            {
                Ready = commentCountReady > 0 ? commentSumReady / commentCountReady : 0,
                Gaps = commentCountGaps > 0 ? commentSumGaps / commentCountGaps : 0,
                NeedsDetail = commentCountNeeds > 0 ? commentSumNeeds / commentCountNeeds : 0,
            },
        };
    }

    private static bool TryGetInt32Property(string? json, string name, out int value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(name, out var prop))
            {
                return false;
            }

            if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt32(out var i))
            {
                value = i;
                return true;
            }

            if (prop.ValueKind == JsonValueKind.String && int.TryParse(prop.GetString(), out var parsed))
            {
                value = parsed;
                return true;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }

    private static bool TryGetStringProperty(string? json, string name, out string value)
    {
        value = "";
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty(name, out var prop))
            {
                return false;
            }

            if (prop.ValueKind == JsonValueKind.String)
            {
                value = prop.GetString() ?? "";
                return value.Length > 0;
            }
        }
        catch (JsonException)
        {
            return false;
        }

        return false;
    }
}
