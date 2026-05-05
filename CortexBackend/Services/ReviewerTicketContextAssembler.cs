using System.Text;
using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

/// <summary>
/// Loads the same deterministic text bundle used by advisory reviewer context features (ticket, board label, integrations).
/// </summary>
public sealed class ReviewerTicketContextAssembler(CortexDbContext db)
{
    public sealed record Bundle(
        string TicketText,
        string? ExternalBlob,
        string? MappingBlob,
        string CombinedText);

    public async Task<Bundle> BuildAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var ticketText = SapTicketReferenceBuildText.BuildTicketText(ticket);

        string? boardLine = null;
        var bd = await db.TicketBoardDefinitions.AsNoTracking()
            .Where(b => b.Id == ticket.BoardId && b.IsEnabled)
            .Select(b => new { b.Name })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (bd?.Name.Trim() is { Length: > 0 } bn)
        {
            boardLine = $"Board context: {bn}";
        }

        var linkedItems = await db.ExternalWorkItems.AsNoTracking()
            .Where(i => i.CortexTicketId == ticket.Id && !i.IsDeleted)
            .OrderByDescending(i => i.LastSeenUtc)
            .Take(24)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        string? externBlob = null;
        if (linkedItems.Count > 0)
        {
            var eb = new StringBuilder();
            foreach (var row in linkedItems)
            {
                AppendLineTrimmed(eb, row.Title);
                AppendLineTrimmed(eb, row.Description);
                AppendLineTrimmed(eb, row.Category);
                AppendLineTrimmed(eb, row.Department);
                AppendLineTrimmed(eb, row.Status);
                AppendLineTrimmed(eb, row.Priority);
            }

            externBlob = eb.Length > 0 ? eb.ToString().Trim() : null;
        }

        string? mappingBlob = null;
        var sourceIds = linkedItems.Select(i => i.ExternalWorkSourceId).Distinct().ToList();
        if (sourceIds.Count > 0)
        {
            var maps = await db.ExternalFieldMappings.AsNoTracking()
                .Where(m => sourceIds.Contains(m.ExternalWorkSourceId))
                .Select(m => new
                {
                    m.ExternalFieldName,
                    m.ExternalFieldKey,
                    m.TransformHint,
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var mb = new StringBuilder();
            foreach (var map in maps)
            {
                AppendLineTrimmed(mb, map.ExternalFieldName);
                AppendLineTrimmed(mb, map.ExternalFieldKey);
                AppendLineTrimmed(mb, map.TransformHint);
            }

            mappingBlob = mb.Length > 0 ? mb.ToString().Trim() : null;
        }

        var combined = SapTicketReferenceBuildText.CombineSections(
            ticketText,
            boardLine,
            externBlob,
            mappingBlob);

        return new Bundle(ticketText, externBlob, mappingBlob, combined);
    }

    private static void AppendLineTrimmed(StringBuilder sb, string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return;
        }

        sb.AppendLine(line.Trim());
    }
}
