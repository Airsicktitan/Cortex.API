using System.Text.Json;
using Cortex.API.Authorization;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Extensions;

/// <summary>
/// Tier 8 admin/control surface: GET summary + PATCH config. Read-only summary is
/// limited to elevated users; configuration writes are admin-only.
/// </summary>
public static class SystemAutonomyEndpoints
{
    public static void MapSystemAutonomyEndpoints(this WebApplication app)
    {
        var autonomy = app.MapGroup("/api/system/autonomy")
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .WithTags("System");

        autonomy.MapGet("/summary", GetSummary)
            .WithName("GetCortexAutonomySummary")
            .Produces<CortexAutonomySummaryResponse>(StatusCodes.Status200OK);

        autonomy.MapPatch("/config", UpdateConfig)
            .RequireAuthorization(CortexAuthorizationExtensions.ElevatedAccess)
            .Accepts<UpdateCortexAutonomySettingsRequest>("application/json")
            .WithName("UpdateCortexAutonomyConfig")
            .Produces<CortexAutonomySettingsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status400BadRequest);
    }

    private static async Task<IResult> GetSummary(
        ICortexAutonomySettingsService settingsService,
        CortexDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var stored = await settingsService.GetStoredAsync(cancellationToken);
        var effective = await settingsService.GetEffectiveAsync(cancellationToken);
        var nowUtc = DateTime.UtcNow;
        var windowStart = nowUtc.AddHours(-24);

        var rows = await dbContext.CortexAutonomyDecisions
            .AsNoTracking()
            .Where(d => d.CreatedDateUtc >= windowStart)
            .Select(d => new
            {
                d.Id,
                d.TicketId,
                d.RecommendedOwnerId,
                d.RecommendedOwnerName,
                d.Mode,
                d.IsEligible,
                d.WasAutoApplied,
                d.Confidence,
                d.PassedChecksJson,
                d.BlockedReasonsJson,
                d.Summary,
                d.CreatedDateUtc,
            })
            .OrderByDescending(d => d.CreatedDateUtc)
            .ThenByDescending(d => d.Id)
            .Take(20)
            .ToListAsync(cancellationToken);

        var counts = await dbContext.CortexAutonomyDecisions
            .AsNoTracking()
            .Where(d => d.CreatedDateUtc >= windowStart)
            .GroupBy(_ => 1)
            .Select(g => new CortexAutonomyCountsResponse
            {
                Evaluated = g.Count(),
                Eligible = g.Count(d => d.IsEligible),
                AutoApplied = g.Count(d => d.WasAutoApplied),
                Blocked = g.Count(d => !d.IsEligible),
            })
            .FirstOrDefaultAsync(cancellationToken)
            ?? new CortexAutonomyCountsResponse();

        var ticketIds = rows.Select(r => r.TicketId).Distinct().ToList();
        var ticketTitleMap = await dbContext.Tickets
            .AsNoTracking()
            .Where(t => ticketIds.Contains(t.Id))
            .Select(t => new { t.Id, t.Title })
            .ToDictionaryAsync(t => t.Id, t => t.Title, cancellationToken);

        var recent = rows
            .Select(row =>
            {
                var (result, label) = ResolveResult(row.WasAutoApplied, row.IsEligible, row.Mode);
                return new CortexAutonomyRecentDecisionResponse
                {
                    TicketId = row.TicketId,
                    TicketTitle = ticketTitleMap.TryGetValue(row.TicketId, out var title) ? title : null,
                    RecommendedOwnerId = row.RecommendedOwnerId,
                    RecommendedOwnerName = row.RecommendedOwnerName,
                    Mode = row.Mode,
                    IsEligible = row.IsEligible,
                    WasAutoApplied = row.WasAutoApplied,
                    Confidence = (double)row.Confidence,
                    Result = result,
                    ResultLabel = label,
                    ReasonSummary = BuildReasonSummary(row.IsEligible, row.WasAutoApplied, row.PassedChecksJson, row.BlockedReasonsJson, row.Summary),
                    EvaluatedAtUtc = row.CreatedDateUtc,
                };
            })
            .ToList();

        return Results.Ok(new CortexAutonomySummaryResponse
        {
            Settings = MapSettings(effective, stored),
            Counts = counts,
            Recent = recent,
            WindowStartUtc = windowStart,
            WindowEndUtc = nowUtc,
        });
    }

    private static async Task<IResult> UpdateConfig(
        UpdateCortexAutonomySettingsRequest? request,
        ICortexAutonomySettingsService settingsService,
        IUserContextService userContextService,
        CancellationToken cancellationToken)
    {
        var body = request ?? new UpdateCortexAutonomySettingsRequest();

        if (body.MinConfidence is { } confidence && (confidence < 0d || confidence > 1d))
        {
            return Results.BadRequest(new { message = "MinConfidence must be between 0 and 1." });
        }
        if (body.MinAlternativeGap is { } gap && (gap < 0d || gap > 1d))
        {
            return Results.BadRequest(new { message = "MinAlternativeGap must be between 0 and 1." });
        }
        if (body.RecentOverrideWindowHours is { } hours && (hours < 0 || hours > 24 * 30))
        {
            return Results.BadRequest(new { message = "RecentOverrideWindowHours must be between 0 and 720." });
        }

        var user = await userContextService.GetCurrentUserAsync();
        var stored = await settingsService.UpdateAsync(body, user.Id, cancellationToken);
        var effective = await settingsService.GetEffectiveAsync(cancellationToken);
        return Results.Ok(MapSettings(effective, stored));
    }

    private static (string Result, string Label) ResolveResult(bool wasApplied, bool isEligible, string mode)
    {
        if (wasApplied)
        {
            return ("AutoApplied", "Auto-applied");
        }

        if (isEligible)
        {
            return mode == "Disabled"
                ? ("Eligible", "Eligible (autonomy off)")
                : ("Eligible", "Eligible (shadow)");
        }

        return ("Blocked", "Blocked");
    }

    private static string BuildReasonSummary(
        bool isEligible,
        bool wasApplied,
        string? passedJson,
        string? blockedJson,
        string fallback)
    {
        if (!isEligible)
        {
            var blocked = SafeDeserialize(blockedJson);
            if (blocked.Count > 0)
            {
                return blocked[0];
            }
        }
        else
        {
            var passed = SafeDeserialize(passedJson);
            if (passed.Count > 0)
            {
                return wasApplied
                    ? $"Applied: {passed[0]}"
                    : passed[0];
            }
        }

        return string.IsNullOrWhiteSpace(fallback) ? "No reason recorded." : fallback;
    }

    private static List<string> SafeDeserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static CortexAutonomySettingsResponse MapSettings(
        Cortex.API.Configuration.CortexAutonomyOptions effective,
        CortexAutonomyConfiguration? stored)
    {
        var mode = effective.Enabled
            ? (effective.ShadowMode ? "Shadow" : "Active")
            : "Disabled";

        return new CortexAutonomySettingsResponse
        {
            Enabled = effective.Enabled,
            ShadowMode = effective.ShadowMode,
            MinConfidence = effective.MinConfidence,
            RecentOverrideWindowHours = effective.RecentOverrideWindowHours,
            RequireClearWinner = effective.RequireClearWinner,
            MinAlternativeGap = effective.MinAlternativeGap,
            LastModifiedDateUtc = stored?.LastModifiedDateUtc,
            LastModifiedByDisplayName = stored?.LastModifiedByUser?.DisplayName,
            Mode = mode,
        };
    }
}
