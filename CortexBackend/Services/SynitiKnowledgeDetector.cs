using System.Text.RegularExpressions;
using Cortex.API.Models;

namespace Cortex.API.Services;

public enum SynitiKnowledgeMatchStrength
{
    Strong = 0,
    Moderate = 1,
}

public readonly record struct SynitiKnowledgeCatalogRow(
    int Id,
    string SourceName,
    string Term,
    SynitiKnowledgeCategory Category,
    string ShortDefinition,
    string? BusinessMeaning,
    string? TechnicalMeaning,
    string? RelatedTerms,
    string? ExamplePhrases,
    string? Aliases,
    string? SuggestedReviewerChecks,
    string? MissingContextQuestions);

public readonly record struct SynitiKnowledgeCandidate(
    SynitiKnowledgeCatalogRow Row,
    SynitiKnowledgeMatchStrength Strength,
    string MatchedPhrase,
    bool MatchedViaExamplePhrase);

/// <summary>Deterministic Syniti/DSP knowledge matching from text (unit-testable).</summary>
public static class SynitiKnowledgeDetector
{
    public const int MaxMatches = 6;

    private static readonly Regex SplitExamples = new(
        @"[\r\n;]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// When ticket text looks like SAP metadata is in scope, prefer migration/governance glossary
    /// matches over generic platform tokens (for example the product name alone) — same detector rules, different ordering.
    /// </summary>
    private static readonly HashSet<string> SapContextBoostTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "MARC", "MARA", "MAKT", "MARD", "MBEW", "WERKS", "MATNR", "MEINS",
        "BUKRS", "LFA1", "KNA1", "EINA", "EINE", "KUNNR", "LIFNR",
        "BSEG", "BKPF", "VBAP", "VBAK", "AFKO", "QMAT",
    };

    public static IReadOnlyList<SynitiKnowledgeCandidate> FindMatches(
        string combinedText,
        IReadOnlyList<SynitiKnowledgeCatalogRow> catalog)
    {
        if (string.IsNullOrWhiteSpace(combinedText) || catalog.Count == 0)
        {
            return [];
        }

        var hay = combinedText.Trim().ToLowerInvariant();
        var unordered = new List<SynitiKnowledgeCandidate>();

        // Longer glossary terms first to prefer specific phrases (e.g. "value mapping" before loose overlaps).
        foreach (var row in catalog.OrderByDescending(r => r.Term.Trim().Length))
        {
            if (TryAddTermOrAliasMatch(hay, row, unordered))
            {
                continue;
            }

            foreach (var ex in ExpandExamplePhrases(row.ExamplePhrases))
            {
                if (!MatchesPhraseDeterministic(hay, ex, out _))
                {
                    continue;
                }

                unordered.Add(new SynitiKnowledgeCandidate(
                    row,
                    SynitiKnowledgeMatchStrength.Moderate,
                    ex.Trim(),
                    MatchedViaExamplePhrase: true));
                break;
            }
        }

        var deduped = new Dictionary<int, SynitiKnowledgeCandidate>();

        foreach (var c in unordered)
        {
            if (!deduped.TryGetValue(c.Row.Id, out var existing))
            {
                deduped[c.Row.Id] = c;
                continue;
            }

            if (c.Strength < existing.Strength)
            {
                deduped[c.Row.Id] = c;
            }
        }

        static int StrengthRank(SynitiKnowledgeMatchStrength s) =>
            s switch
            {
                SynitiKnowledgeMatchStrength.Strong => 0,
                _ => 1,
            };

        var sapContextLikely = HasSapCatalogContextSignal(combinedText);

        static int EffectiveStrengthRank(SynitiKnowledgeCandidate c, bool sapLikely)
        {
            var r = StrengthRank(c.Strength);
            if (sapLikely &&
                c.Strength == SynitiKnowledgeMatchStrength.Strong &&
                c.Row.Category == SynitiKnowledgeCategory.Platform)
            {
                // Keeps the same match set; only deprioritizes generic product-name hits when SAP metadata is in play.
                return 1;
            }

            return r;
        }

        return deduped.Values
            .OrderBy(c => EffectiveStrengthRank(c, sapContextLikely))
            .ThenByDescending(c => GovernanceCategoryPriorityWhenSapLikely(c.Row.Category, sapContextLikely))
            .ThenByDescending(c => c.Row.Term.Trim().Length)
            .Take(MaxMatches)
            .ToList();
    }

    private static bool HasSapCatalogContextSignal(string combinedText)
    {
        if (string.IsNullOrWhiteSpace(combinedText))
        {
            return false;
        }

        var hay = combinedText.Trim().ToUpperInvariant();
        foreach (var token in SapContextBoostTokens)
        {
            var esc = Regex.Escape(token);
            if (Regex.IsMatch(
                    hay,
                    $@"(?<![A-Z0-9_]){esc}(?![A-Z0-9_])",
                    RegexOptions.CultureInvariant))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Higher values sort earlier when <paramref name="sapContextLikely"/> is true.
    /// </summary>
    private static int GovernanceCategoryPriorityWhenSapLikely(
        SynitiKnowledgeCategory category,
        bool sapContextLikely)
    {
        if (!sapContextLikely)
        {
            return 0;
        }

        return category switch
        {
            SynitiKnowledgeCategory.Platform => 0,
            SynitiKnowledgeCategory.Module or SynitiKnowledgeCategory.Job => 4,
            _ => 10,
        };
    }

    /// <returns><c>true</c> when a strong (term/alias) match was added and example phrases should be skipped.</returns>
    private static bool TryAddTermOrAliasMatch(
        string hayLower,
        SynitiKnowledgeCatalogRow row,
        List<SynitiKnowledgeCandidate> unordered)
    {
        if (MatchesPhraseDeterministic(hayLower, row.Term, out _))
        {
            unordered.Add(new SynitiKnowledgeCandidate(
                row,
                SynitiKnowledgeMatchStrength.Strong,
                row.Term.Trim(),
                MatchedViaExamplePhrase: false));
            return true;
        }

        foreach (var a in ExpandAliases(row.Aliases))
        {
            if (!MatchesPhraseDeterministic(hayLower, a, out _))
            {
                continue;
            }

            unordered.Add(new SynitiKnowledgeCandidate(
                row,
                SynitiKnowledgeMatchStrength.Strong,
                a.Trim(),
                MatchedViaExamplePhrase: false));
            return true;
        }

        return false;
    }

    private static IEnumerable<string> ExpandAliases(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length >= 2);
    }

    private static IEnumerable<string> ExpandExamplePhrases(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return SplitExamples.Split(raw)
            .Select(s => s.Trim())
            .Where(s => s.Length >= 3);
    }

    /// <summary>
    /// Multi-word phrases: case-insensitive substring.
    /// Single-token phrases: non-alphanumeric word boundaries to reduce accidental hits.
    /// </summary>
    internal static bool MatchesPhraseDeterministic(string hayLower, string phrase, out bool usedWordBoundary)
    {
        usedWordBoundary = false;
        var p = phrase.Trim();
        if (p.Length == 0)
        {
            return false;
        }

        var pl = p.ToLowerInvariant();

        if (pl.IndexOf(' ') >= 0)
        {
            return hayLower.Contains(pl, StringComparison.Ordinal);
        }

        if (pl.Length < 2)
        {
            return false;
        }

        usedWordBoundary = true;
        var esc = Regex.Escape(pl);
        return Regex.IsMatch(
            hayLower,
            $@"(?<![a-z0-9_]){esc}(?![a-z0-9_])",
            RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    }
}
