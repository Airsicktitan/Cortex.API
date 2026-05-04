using System.Text.RegularExpressions;
using Cortex.API.DTO;

namespace Cortex.API.Services;

/// <summary>
/// Deterministic SAP reference detection from ticket text and catalog snapshots (unit-testable).
/// </summary>
public static class SapTicketReferenceDetector
{
    private static readonly HashSet<string> NoContextFieldNames = new(StringComparer.Ordinal)
    {
        "ID", "TYPE", "STATUS", "NAME", "TEXT",
    };

    private static readonly Regex TableFieldPattern = new(
        @"\b([A-Z][A-Z0-9]{2,})\s*[-. ]\s*([A-Z][A-Z0-9_]{0,})\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public const int MaxMatches = 10;

    public static SapTicketReferenceContextDto DetectForTicket(
        string ticketId,
        string combinedText,
        IReadOnlyList<SapTicketCatalogTable> tables,
        IReadOnlyList<SapTicketCatalogField> fields)
    {
        var matches = DetectMatches(combinedText, tables, fields);
        var sapIntentOnly = matches.Count == 0 && SapTicketSapIntentAnalyzer.HasSapIntent(combinedText);
        return new SapTicketReferenceContextDto(ticketId, matches, sapIntentOnly);
    }

    public static IReadOnlyList<SapTicketReferenceMatchDto> DetectMatches(
        string combinedText,
        IReadOnlyList<SapTicketCatalogTable> tables,
        IReadOnlyList<SapTicketCatalogField> fields)
    {
        if (string.IsNullOrWhiteSpace(combinedText) || tables.Count == 0 && fields.Count == 0)
        {
            return [];
        }

        var upperFull = combinedText.Trim().ToUpperInvariant();
        var tokenSet = new HashSet<string>(ExtractTokens(upperFull), StringComparer.Ordinal);

        var tableByUpperName = new Dictionary<string, List<SapTicketCatalogTable>>(StringComparer.Ordinal);
        foreach (var t in tables)
        {
            var key = t.TableName.Trim().ToUpperInvariant();
            if (!tableByUpperName.TryGetValue(key, out var list))
            {
                list = [];
                tableByUpperName[key] = list;
            }

            list.Add(t);
        }

        var fieldsByUpperName = new Dictionary<string, List<SapTicketCatalogField>>(StringComparer.Ordinal);
        foreach (var f in fields)
        {
            var key = f.FieldName.Trim().ToUpperInvariant();
            if (!fieldsByUpperName.TryGetValue(key, out var list))
            {
                list = [];
                fieldsByUpperName[key] = list;
            }

            list.Add(f);
        }

        var accumulator = new List<SapTicketReferenceMatchDto>();

        foreach (var (tableUpper, tableRows) in tableByUpperName)
        {
            if (tokenSet.Contains(tableUpper))
            {
                foreach (var row in tableRows)
                {
                    accumulator.Add(CreateTableMatch(
                        matchedText: row.TableName,
                        row,
                        SapTicketReferenceMatchConfidence.High,
                        "Exact token match to SAP table name in enabled catalog."));
                }
            }
        }

        foreach (Match m in TableFieldPattern.Matches(upperFull))
        {
            if (m.Groups.Count < 3)
            {
                continue;
            }

            var tPart = m.Groups[1].Value;
            var fPart = m.Groups[2].Value;
            if (string.IsNullOrEmpty(tPart) || string.IsNullOrEmpty(fPart))
            {
                continue;
            }

            if (!tableByUpperName.TryGetValue(tPart, out var tRows))
            {
                continue;
            }

            var matchedExpression = $"{tPart}-{fPart}";
            var hadTableToken = tokenSet.Contains(tPart);

            foreach (var tRow in tRows)
            {
                if (!hadTableToken)
                {
                    accumulator.Add(CreateTableMatch(
                        matchedText: matchedExpression,
                        tRow,
                        SapTicketReferenceMatchConfidence.Medium,
                        "SAP table name appears in a table-field style expression in the ticket."));
                }

                SapTicketCatalogField? exprField = null;
                foreach (var f in fields)
                {
                    if (f.TableMetadataId == tRow.Id &&
                        string.Equals(f.FieldName.Trim(), fPart, StringComparison.OrdinalIgnoreCase))
                    {
                        exprField = f;
                        break;
                    }
                }

                if (exprField is { } hit)
                {
                    accumulator.Add(CreateFieldMatchFromCatalog(
                        matchedText: matchedExpression,
                        hit,
                        SapTicketReferenceMatchConfidence.High,
                        "Table-field expression matches a known table and field in the catalog."));
                }
            }
        }

        foreach (var token in tokenSet)
        {
            if (!fieldsByUpperName.TryGetValue(token, out var candidates) || candidates.Count == 0)
            {
                continue;
            }

            if (!ShouldConsiderFieldToken(token, candidates, tokenSet, out var resolved))
            {
                continue;
            }

            foreach (var fieldRow in resolved)
            {
                var conf = tokenSet.Contains(fieldRow.TableName.Trim().ToUpperInvariant())
                    ? SapTicketReferenceMatchConfidence.High
                    : candidates.Count == 1
                        ? SapTicketReferenceMatchConfidence.High
                        : SapTicketReferenceMatchConfidence.Medium;

                var reason = tokenSet.Contains(fieldRow.TableName.Trim().ToUpperInvariant())
                    ? "Field token matches catalog metadata with table name present in the ticket."
                    : candidates.Count == 1
                        ? "Field token matches a uniquely cataloged SAP field in enabled sources."
                        : "Field token matches catalog metadata with strong disambiguation.";

                accumulator.Add(CreateFieldMatchFromCatalog(token, fieldRow, conf, reason));
            }
        }

        return DedupeAndRank(accumulator).Take(MaxMatches).ToList();
    }

    private static bool ShouldConsiderFieldToken(
        string token,
        List<SapTicketCatalogField> candidates,
        HashSet<string> tokenSet,
        out IReadOnlyList<SapTicketCatalogField> resolved)
    {
        resolved = [];
        var upperToken = token.Trim().ToUpperInvariant();

        if (NoContextFieldNames.Contains(upperToken))
        {
            var withTable = candidates
                .Where(c => tokenSet.Contains(c.TableName.Trim().ToUpperInvariant()))
                .ToList();
            if (withTable.Count == 0)
            {
                return false;
            }

            resolved = withTable;
            return true;
        }

        if (upperToken.Length < 4)
        {
            var withTable = candidates
                .Where(c => tokenSet.Contains(c.TableName.Trim().ToUpperInvariant()))
                .ToList();
            if (withTable.Count == 0)
            {
                return false;
            }

            resolved = withTable;
            return true;
        }

        var tablesInText = candidates
            .Where(c => tokenSet.Contains(c.TableName.Trim().ToUpperInvariant()))
            .ToList();
        if (tablesInText.Count > 0)
        {
            resolved = tablesInText;
            return true;
        }

        if (candidates.Count == 1)
        {
            resolved = candidates;
            return true;
        }

        return false;
    }

    private static List<SapTicketReferenceMatchDto> DedupeAndRank(List<SapTicketReferenceMatchDto> raw)
    {
        var best = new Dictionary<string, SapTicketReferenceMatchDto>(StringComparer.Ordinal);
        foreach (var m in raw)
        {
            var key = $"{(int)m.MatchType}|{m.TableName ?? ""}|{m.FieldName ?? ""}|{m.SourceId?.ToString() ?? ""}";
            if (!best.TryGetValue(key, out var existing) || ConfidenceRank(m.Confidence) < ConfidenceRank(existing.Confidence))
            {
                best[key] = m;
            }
            else if (ConfidenceRank(m.Confidence) == ConfidenceRank(existing.Confidence) &&
                     string.Compare(m.Reason, existing.Reason, StringComparison.Ordinal) < 0)
            {
                best[key] = m;
            }
        }

        var list = best.Values.ToList();
        list.Sort(static (a, b) =>
        {
            var typeCmp = MatchTypeSort(a.MatchType).CompareTo(MatchTypeSort(b.MatchType));
            if (typeCmp != 0)
            {
                return typeCmp;
            }

            var confCmp = ConfidenceRank(a.Confidence).CompareTo(ConfidenceRank(b.Confidence));
            if (confCmp != 0)
            {
                return confCmp;
            }

            return string.Compare(
                a.TableName + a.FieldName + a.MatchedText,
                b.TableName + b.FieldName + b.MatchedText,
                StringComparison.Ordinal);
        });
        return list;
    }

    private static int MatchTypeSort(SapTicketReferenceMatchType t) => t switch
    {
        SapTicketReferenceMatchType.Table => 0,
        SapTicketReferenceMatchType.Field => 1,
        _ => 2,
    };

    private static int ConfidenceRank(SapTicketReferenceMatchConfidence c) => c switch
    {
        SapTicketReferenceMatchConfidence.High => 0,
        SapTicketReferenceMatchConfidence.Medium => 1,
        _ => 2,
    };

    private static SapTicketReferenceMatchDto CreateTableMatch(
        string matchedText,
        SapTicketCatalogTable row,
        SapTicketReferenceMatchConfidence confidence,
        string reason) =>
        new(
            SapTicketReferenceMatchType.Table,
            MatchedText: matchedText,
            TableName: row.TableName,
            TableDescription: row.Description,
            FieldName: null,
            FieldDescription: null,
            DomainName: null,
            DomainValue: null,
            SourceName: row.SourceName,
            Module: row.Module,
            BusinessObject: row.BusinessObject,
            DataDomain: row.DataDomain,
            IsCustom: row.IsCustom,
            confidence,
            Reason: reason,
            TableId: row.Id,
            FieldId: null,
            SourceId: row.SourceId);

    private static SapTicketReferenceMatchDto CreateFieldMatchFromCatalog(
        string matchedText,
        SapTicketCatalogField f,
        SapTicketReferenceMatchConfidence confidence,
        string reason) =>
        new(
            SapTicketReferenceMatchType.Field,
            MatchedText: matchedText,
            TableName: f.TableName,
            TableDescription: f.TableDescription,
            FieldName: f.FieldName,
            FieldDescription: f.FieldDescription,
            DomainName: null,
            DomainValue: null,
            SourceName: f.SourceName,
            Module: f.Module,
            BusinessObject: f.BusinessObject,
            DataDomain: f.DataDomain,
            IsCustom: f.FieldIsCustom,
            confidence,
            Reason: reason,
            TableId: f.TableMetadataId,
            FieldId: f.Id,
            SourceId: f.SourceId);

    private static IEnumerable<string> ExtractTokens(string upperTextWithSpaces)
    {
        var normalized = Regex.Replace(upperTextWithSpaces, @"[-.]+", " ");
        foreach (var part in Regex.Split(normalized, @"[^A-Z0-9_]+"))
        {
            var t = part.Trim();
            if (t.Length > 0)
            {
                yield return t;
            }
        }
    }
}
