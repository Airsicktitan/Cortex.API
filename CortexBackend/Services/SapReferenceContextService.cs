using System.Text.RegularExpressions;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

/// <inheritdoc />
public sealed class SapReferenceContextService(
    CortexDbContext db,
    ReviewerTicketContextAssembler contextAssembler) : ISapReferenceContextService
{
    private static readonly Regex SpaceCollapseRegex = new(
        @"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);


    public async Task<SapTicketReferenceContextDto> DetectSapReferencesForTicketAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var segments = await contextAssembler.BuildAsync(ticket, cancellationToken).ConfigureAwait(false);
        var (tables, fields) = await LoadEnabledCatalogAsync(cancellationToken).ConfigureAwait(false);

        var raw = SapTicketReferenceDetector.DetectForTicket(
            ticket.Id,
            segments.CombinedText,
            tables,
            fields);

        var composed = raw.Matches.ToList();
        composed = await ApplyDomainPreviewAsync(composed, cancellationToken).ConfigureAwait(false);
        composed = ComposeMatches(composed, segments);

        return raw with { Matches = composed };
    }



    private async Task<(List<SapTicketCatalogTable> Tables, List<SapTicketCatalogField> Fields)> LoadEnabledCatalogAsync(
        CancellationToken cancellationToken)
    {
        var tables = await db.SapTables.AsNoTracking()
            .Where(t => t.SapReferenceSource.IsEnabled)
            .Select(t => new SapTicketCatalogTable(
                t.Id,
                t.SapReferenceSourceId,
                t.SapReferenceSource.Name,
                t.TableName,
                t.Description,
                t.Module,
                t.BusinessObject,
                t.DataDomain,
                t.IsCustom))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var fields = await db.SapFields.AsNoTracking()
            .Where(f => f.SapTableMetadata.SapReferenceSource.IsEnabled)
            .Select(f => new SapTicketCatalogField(
                f.Id,
                f.SapTableMetadataId,
                f.SapTableMetadata.SapReferenceSourceId,
                f.SapTableMetadata.SapReferenceSource.Name,
                f.SapTableMetadata.TableName,
                f.SapTableMetadata.Description,
                f.SapTableMetadata.Module,
                f.SapTableMetadata.BusinessObject,
                f.SapTableMetadata.DataDomain,
                f.SapTableMetadata.IsCustom,
                f.FieldName,
                f.Description ?? f.BusinessMeaning,
                f.DomainName,
                f.IsCustom))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return (tables, fields);
    }

    private async Task<List<SapTicketReferenceMatchDto>> ApplyDomainPreviewAsync(
        List<SapTicketReferenceMatchDto> matches,
        CancellationToken cancellationToken)
    {
        if (matches.Count == 0)
        {
            return matches;
        }

        var keysNeededArr = matches
            .Where(m => !string.IsNullOrWhiteSpace(m.DomainName) && m.SourceId.HasValue)
            .Select(m => (SourceId: m.SourceId!.Value, DomainUpper: m.DomainName!.Trim().ToUpperInvariant()))
            .Distinct()
            .ToHashSet();

        if (keysNeededArr.Count == 0)
        {
            return matches;
        }

        var sourceIdsSet = keysNeededArr.Select(k => k.SourceId).Distinct().ToHashSet();

        var rows = await db.SapDomainValues.AsNoTracking()
            .Where(v =>
                sourceIdsSet.Contains(v.SapReferenceSourceId) &&
                v.SapReferenceSource.IsEnabled)
            .OrderBy(v => v.DomainName)
            .ThenBy(v => v.Value)
            .Select(v => new
            {
                v.SapReferenceSourceId,
                v.DomainName,
                v.Value,
                v.Description,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var keyed = rows
            .GroupBy(v => (
                SourceId: v.SapReferenceSourceId,
                DomainUpper: v.DomainName.Trim().ToUpperInvariant()));

        Dictionary<(int SourceId, string DomainUpper), string> previews = [];

        foreach (var g in keyed)
        {
            if (!keysNeededArr.Contains(g.Key))
            {
                continue;
            }

            var ordered = g.OrderBy(r => r.Value, StringComparer.OrdinalIgnoreCase).ToList();

            var parts = ordered.Take(5).Select(row =>
                {
                    var v = row.Value.Trim();
                    var d = row.Description?.Trim();
                    return string.IsNullOrEmpty(d) ? v : $"{v} ({Truncate(d, 80)})";
                })
                .ToList();

            if (parts.Count == 0)
            {
                continue;
            }

            var line = string.Join(", ", parts);
            previews[g.Key] = ordered.Count > parts.Count
                ? $"{line} (+additional values omitted)"
                : line;
        }

        List<SapTicketReferenceMatchDto> next = [];

        foreach (var m in matches)
        {
            if (string.IsNullOrWhiteSpace(m.DomainName) || !m.SourceId.HasValue)
            {
                next.Add(m);
                continue;
            }

            var kk = (
                SourceId: m.SourceId!.Value,
                DomainUpper: m.DomainName.Trim().ToUpperInvariant());

            next.Add(previews.TryGetValue(kk, out var pv)
                ? m with { DomainValuesPreview = pv }
                : m);
        }

        return next;
    }

    private static List<SapTicketReferenceMatchDto> ComposeMatches(
        IReadOnlyList<SapTicketReferenceMatchDto> matches,
        ReviewerTicketContextAssembler.Bundle segments) =>
        matches.Count == 0
            ? []
            : matches.Select(m => ComposeSingle(m, segments)).ToList();

    private static SapTicketReferenceMatchDto ComposeSingle(SapTicketReferenceMatchDto m, ReviewerTicketContextAssembler.Bundle segments)
    {
        var needles = ProbeNeedles(m);

        var ticketHit = HitsSegment(segments.TicketText, needles);
        var externHit = HitsSegment(segments.ExternalBlob, needles);
        var mappingHit = HitsSegment(segments.MappingBlob, needles);

        var sourceReason = BuildSourceReason(m, ticketHit, externHit, mappingHit);

        return m with { SourceReason = sourceReason };
    }

    private static string BuildSourceReason(
        SapTicketReferenceMatchDto m,
        bool ticketHit,
        bool externHit,
        bool mappingHit)
    {
        var focus = ComposeFocusPhrase(m);

        string locationClause;
        if (!ticketHit && !externHit && !mappingHit)
        {
            locationClause = ", based on Cortex’s combined request scan (ticket, board, and linked integrations).";
        }
        else if (ticketHit && !externHit && !mappingHit)
        {
            locationClause = " in the request details.";
        }
        else
        {
            var bits = new List<string>();

            void AddHit(bool ok, string label)
            {
                if (ok)
                {
                    bits.Add(label);
                }
            }

            AddHit(ticketHit, "the ticket wording");
            AddHit(externHit, "linked external item context");
            AddHit(mappingHit, "configured external field mappings");

            var joined =
                bits.Count <= 2
                    ? string.Join(" and ", bits)
                    : string.Join(", ", bits[..^1]) + ", and " + bits[^1];
            locationClause = $" while correlating wording from {joined}.";
        }

        var headline = $"Cortex found SAP {focus}{locationClause}";
        var strength =
            $"{m.MatchStrengthLabel} match aligns with deterministic catalog metadata from “{m.SourceName}”.";

        var tailPieces = ComposeMeaningNotes(m).Where(static s => !string.IsNullOrWhiteSpace(s)).ToList();

        var body = $"{headline} {string.Join(' ', tailPieces)} {strength}".Trim();

        return CollapseSpaces(body);
    }

    private static IEnumerable<string> ComposeMeaningNotes(SapTicketReferenceMatchDto m)
    {
        if (IndicatesPurchasingInfoRecord(m))
        {
            yield return
                "Imported metadata aligns this wording with Purchasing Info Records (commonly SAP hierarchy tables such as EINA and EINE).";
        }

        if (m.MatchType != SapTicketReferenceMatchType.Table &&
            SapTicketReferenceDetector.IsLikelyCustomerExtension(m.FieldName ?? string.Empty, m.IsCustom))
        {
            yield return "The field naming pattern suggests this is likely a customer or extension-oriented SAP field.";
        }

        var meaning = PreferMeaningSentence(m);

        if (!string.IsNullOrWhiteSpace(meaning))
        {
            yield return meaning.Trim();
        }

        if (!string.IsNullOrWhiteSpace(m.DomainValuesPreview))
        {
            yield return $"Domain preview (catalog): {m.DomainValuesPreview}";
        }
    }

    private static string? PreferMeaningSentence(SapTicketReferenceMatchDto m)
    {
        var snippet = ChooseMeaningSnippet(m);
        return string.IsNullOrWhiteSpace(snippet)
            ? null
            : $"Meaning snapshot: {Truncate(snippet.Trim(), 240)}.";
    }

    private static string? ChooseMeaningSnippet(SapTicketReferenceMatchDto m) =>
        m.MatchType switch
        {
            SapTicketReferenceMatchType.Field =>
                PreferLongerNonEmpty(m.FieldDescription, m.TableDescription),
            _ => PreferLongerNonEmpty(m.TableDescription, m.BusinessObject, m.Module),
        };

    private static string? PreferLongerNonEmpty(params string?[] chunks) =>
        chunks.Where(static c => !string.IsNullOrWhiteSpace(c)).OrderByDescending(c => c!.Length).FirstOrDefault();

    private static bool IndicatesPurchasingInfoRecord(SapTicketReferenceMatchDto m) =>
        IsPurchasingInfoRecordTables(m.TableName) || PurchasingBusinessObjectMatches(m.BusinessObject);

    private static bool PurchasingBusinessObjectMatches(string? businessObject)
    {
        if (string.IsNullOrWhiteSpace(businessObject))
        {
            return false;
        }

        return businessObject.Contains("purchasing info record", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsPurchasingInfoRecordTables(string? tableUpperOrRaw)
    {
        if (string.IsNullOrWhiteSpace(tableUpperOrRaw))
        {
            return false;
        }

        var t = tableUpperOrRaw.Trim().ToUpperInvariant();
        return t is "EINA" or "EINE";
    }

    private static HashSet<string> ProbeNeedles(SapTicketReferenceMatchDto m)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in ExpandNeedlePieces(m))
        {
            var t = part.Trim();
            if (t.Length >= 3)
            {
                set.Add(t);
            }
        }

        return set;
    }

    private static IEnumerable<string> ExpandNeedlePieces(SapTicketReferenceMatchDto m)
    {
        yield return m.MatchedText;

        var table = m.TableName?.Trim();
        var field = m.FieldName?.Trim();

        if (!string.IsNullOrEmpty(table))
        {
            yield return table;
        }

        if (!string.IsNullOrEmpty(field))
        {
            yield return field;

            // Expression style "MARC-YYNGM_ACTIVE" — match both segments even if hyphen split elsewhere.
            if (!string.IsNullOrEmpty(table))
            {
                yield return $"{table}-{field}";
                yield return $"{table} / {field}";
            }
        }
    }

    private static bool HitsSegment(string? segment, HashSet<string> needles)
    {
        if (string.IsNullOrWhiteSpace(segment) || needles.Count == 0)
        {
            return false;
        }

        foreach (var needle in needles)
        {
            if (segment.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string ComposeFocusPhrase(SapTicketReferenceMatchDto m)
    {
        var table = m.TableName?.Trim();
        var field = m.FieldName?.Trim();

        return m.MatchType switch
        {
            SapTicketReferenceMatchType.Table when string.IsNullOrEmpty(table)
                => "table context",

            SapTicketReferenceMatchType.Table => $"table {table}",

            _ when !string.IsNullOrEmpty(field) && !string.IsNullOrEmpty(table) => $"{table} / {field}",

            _ when !string.IsNullOrEmpty(field) => $"field {field}",

            _ when !string.IsNullOrEmpty(table) => $"{table}",

            _ => "reference context",
        };
    }

    private static string CollapseSpaces(string value) => SpaceCollapseRegex.Replace(value.Trim(), " ");

    private static string Truncate(string text, int max)
    {
        var t = text.Trim();
        if (t.Length <= max)
        {
            return t;
        }

        return string.Concat(t.AsSpan(0, Math.Max(1, max - 1)), "…");
    }
}
