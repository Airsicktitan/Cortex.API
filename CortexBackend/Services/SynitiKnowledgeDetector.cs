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
    string? ExamplePhrases);

public readonly record struct SynitiKnowledgeCandidate(
    SynitiKnowledgeCatalogRow Row,
    SynitiKnowledgeMatchStrength Strength,
    string MatchedPhrase,
    bool MatchedViaExamplePhrase);

/// <summary>Deterministic Syniti/DSP knowledge matching from text (unit-testable).</summary>
public static class SynitiKnowledgeDetector
{
    public const int MaxMatches = 5;

    private static readonly Regex SplitExamples = new(
        @"[\r\n;]+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
            if (MatchesPhraseDeterministic(hay, row.Term, out var viaBoundary))
            {
                _ = viaBoundary;
                unordered.Add(new SynitiKnowledgeCandidate(
                    row,
                    SynitiKnowledgeMatchStrength.Strong,
                    row.Term.Trim(),
                    MatchedViaExamplePhrase: false));
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

        return deduped.Values
            .OrderBy(c => StrengthRank(c.Strength))
            .ThenByDescending(c => c.Row.Term.Trim().Length)
            .Take(MaxMatches)
            .ToList();
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
