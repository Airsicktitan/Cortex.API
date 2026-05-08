using System.Text.RegularExpressions;
using Cortex.API.DTO;

namespace Cortex.API.Services;

/// <summary>Deterministic search ranking for read-only catalog visibility (lower sort key = stronger match).</summary>
public static class CatalogSearchRanking
{
    private static readonly char[] Delimiters = ['|', ';', '\n', '\r'];

    public static string NormalizeSearchText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var s = input.Trim().ToLowerInvariant();
        return Regex.Replace(s, @"\s+", " ", RegexOptions.CultureInvariant);
    }

    /// <summary>When <paramref name="normalizedQuery"/> is empty, returns 0 (caller should sort by term only).</summary>
    public static int GetSynitiSortKey(SynitiKnowledgeCatalogEntryDto dto, string normalizedQuery)
    {
        if (normalizedQuery.Length == 0)
        {
            return 0;
        }

        var term = NormalizeSearchText(dto.Term);
        var category = NormalizeSearchText(dto.Category);
        var best = int.MaxValue;

        if (term == normalizedQuery)
        {
            best = Math.Min(best, 100);
        }

        if (term.StartsWith(normalizedQuery, StringComparison.Ordinal))
        {
            best = Math.Min(best, 200);
        }

        if (category == normalizedQuery)
        {
            best = Math.Min(best, 300);
        }

        foreach (var phrase in SplitPhrases(dto.Aliases))
        {
            if (phrase == normalizedQuery)
            {
                best = Math.Min(best, 300);
            }
        }

        foreach (var phrase in SplitPhrases(dto.ExamplePhrases))
        {
            if (phrase == normalizedQuery)
            {
                best = Math.Min(best, 300);
            }
        }

        if (term.Contains(normalizedQuery, StringComparison.Ordinal) && term != normalizedQuery &&
            !term.StartsWith(normalizedQuery, StringComparison.Ordinal))
        {
            best = Math.Min(best, 400);
        }

        if (PhraseListContainsSubstring(dto.Aliases, normalizedQuery) ||
            PhraseListContainsSubstring(dto.ExamplePhrases, normalizedQuery))
        {
            best = Math.Min(best, 500);
        }

        if (ContainsNormalized(dto.Aliases, normalizedQuery) ||
            ContainsNormalized(dto.ExamplePhrases, normalizedQuery))
        {
            best = Math.Min(best, 500);
        }

        if (GuidanceOrRelatedContains(dto, normalizedQuery))
        {
            best = Math.Min(best, 600);
        }

        return best == int.MaxValue ? 650 : best;
    }

    /// <summary>When <paramref name="normalizedQuery"/> is empty, returns 0 (caller uses default catalog order).</summary>
    public static int GetSapSortKey(SapReferenceCatalogEntryDto dto, string normalizedQuery)
    {
        if (normalizedQuery.Length == 0)
        {
            return 0;
        }

        var table = NormalizeSearchText(dto.TableName);
        var field = dto.FieldName != null ? NormalizeSearchText(dto.FieldName) : string.Empty;
        var isTable = dto.RowKind.Equals("Table", StringComparison.OrdinalIgnoreCase);

        var best = int.MaxValue;

        if (isTable && table == normalizedQuery)
        {
            best = Math.Min(best, 100);
        }

        if (!isTable && field == normalizedQuery)
        {
            best = Math.Min(best, 110);
        }

        if (!isTable && table == normalizedQuery && field.Length > 0 && field != normalizedQuery)
        {
            best = Math.Min(best, 120);
        }

        var startsTable = table.StartsWith(normalizedQuery, StringComparison.Ordinal) &&
                          table != normalizedQuery;
        var startsField = field.Length > 0 &&
                           field.StartsWith(normalizedQuery, StringComparison.Ordinal) &&
                           field != normalizedQuery;
        if (startsTable || startsField)
        {
            var sub = isTable ? 0 : 1;
            best = Math.Min(best, 200 + sub);
        }

        if (ContextExactMatch(dto, normalizedQuery))
        {
            best = Math.Min(best, 300);
        }

        var nameContains =
            (table.Contains(normalizedQuery, StringComparison.Ordinal) && table != normalizedQuery &&
             !table.StartsWith(normalizedQuery, StringComparison.Ordinal)) ||
            (field.Length > 0 && field.Contains(normalizedQuery, StringComparison.Ordinal) &&
             field != normalizedQuery && !field.StartsWith(normalizedQuery, StringComparison.Ordinal));
        if (nameContains)
        {
            best = Math.Min(best, 400);
        }

        if (DescriptionOrContextContains(dto, normalizedQuery))
        {
            best = Math.Min(best, 500);
        }

        if (SourceFieldsContain(dto, normalizedQuery))
        {
            best = Math.Min(best, 600);
        }

        return best;
    }

    public static bool SapEntryMatchesSearch(SapReferenceCatalogEntryDto dto, string normalizedQuery)
    {
        if (normalizedQuery.Length == 0)
        {
            return true;
        }

        return GetSapSortKey(dto, normalizedQuery) < int.MaxValue;
    }

    private static bool ContextExactMatch(SapReferenceCatalogEntryDto dto, string q)
    {
        var m = NormalizeSearchText(dto.Module);
        var d = NormalizeSearchText(dto.Domain);
        var b = NormalizeSearchText(dto.BusinessObject);
        return (m.Length > 0 && m == q) || (d.Length > 0 && d == q) || (b.Length > 0 && b == q);
    }

    private static bool DescriptionOrContextContains(SapReferenceCatalogEntryDto dto, string q)
    {
        return ContainsNormalized(dto.TableDescription, q) ||
               ContainsNormalized(dto.FieldDescription, q) ||
               ContextSubstringMatch(dto, q);
    }

    private static bool ContextSubstringMatch(SapReferenceCatalogEntryDto dto, string q)
    {
        var parts = new[] { dto.Module, dto.Domain, dto.BusinessObject };
        foreach (var p in parts)
        {
            var n = NormalizeSearchText(p);
            if (n.Length > 0 && n.Contains(q, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool SourceFieldsContain(SapReferenceCatalogEntryDto dto, string q)
    {
        return ContainsNormalized(dto.SourceName, q) || ContainsNormalized(dto.SourceType, q);
    }

    private static bool GuidanceOrRelatedContains(SynitiKnowledgeCatalogEntryDto dto, string q)
    {
        if (ContainsNormalized(dto.BusinessMeaning, q) ||
            ContainsNormalized(dto.ShortDefinition, q) ||
            ContainsNormalized(dto.TechnicalMeaning, q) ||
            ContainsNormalized(dto.RelatedTerms, q))
        {
            return true;
        }

        foreach (var line in dto.SuggestedReviewerChecks)
        {
            if (NormalizeSearchText(line).Contains(q, StringComparison.Ordinal))
            {
                return true;
            }
        }

        foreach (var line in dto.MissingContextQuestions)
        {
            if (NormalizeSearchText(line).Contains(q, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool PhraseListContainsSubstring(string? raw, string q)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        foreach (var phrase in SplitPhrases(raw))
        {
            if (phrase != q && phrase.Contains(q, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsNormalized(string? text, string q)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        return NormalizeSearchText(text).Contains(q, StringComparison.Ordinal);
    }

    private static IEnumerable<string> SplitPhrases(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            yield break;
        }

        foreach (var segment in raw.Split(Delimiters, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var n = NormalizeSearchText(segment);
            if (n.Length > 0)
            {
                yield return n;
            }
        }
    }
}
