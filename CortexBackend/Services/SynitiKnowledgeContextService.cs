using System.Text.RegularExpressions;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public interface ISynitiKnowledgeContextService
{
    Task<SynitiKnowledgeContextDto> GetContextForTicketAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public sealed class SynitiKnowledgeContextService(
    CortexDbContext db,
    ReviewerTicketContextAssembler contextAssembler) : ISynitiKnowledgeContextService
{
    private static readonly Regex SpaceCollapse = new(
        @"\s+",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public async Task<SynitiKnowledgeContextDto> GetContextForTicketAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ticket);

        var bundle = await contextAssembler.BuildAsync(ticket, cancellationToken).ConfigureAwait(false);
        var catalog = await LoadCatalogAsync(cancellationToken).ConfigureAwait(false);
        var hits = SynitiKnowledgeDetector.FindMatches(bundle.CombinedText, catalog);

        if (hits.Count == 0)
        {
            return new SynitiKnowledgeContextDto(ticket.Id, []);
        }

        List<SynitiKnowledgeContextMatchDto> matches = [];

        foreach (var h in hits)
        {
            cancellationToken.ThrowIfCancellationRequested();

            matches.Add(BuildMatchDto(h, bundle));
        }

        return new SynitiKnowledgeContextDto(ticket.Id, matches);
    }

    private async Task<List<SynitiKnowledgeCatalogRow>> LoadCatalogAsync(CancellationToken cancellationToken)
    {
        var rows = await db.SynitiKnowledgeEntries.AsNoTracking()
            .Where(e => e.SynitiKnowledgeSource.IsEnabled)
            .OrderBy(e => e.Term)
            .Select(e => new SynitiKnowledgeCatalogRow(
                e.Id,
                e.SynitiKnowledgeSource.Name,
                e.Term,
                e.Category,
                e.ShortDefinition,
                e.BusinessMeaning,
                e.TechnicalMeaning,
                e.RelatedTerms,
                e.ExamplePhrases,
                e.Aliases,
                e.SuggestedReviewerChecks,
                e.MissingContextQuestions))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows;
    }

    private SynitiKnowledgeContextMatchDto BuildMatchDto(
        SynitiKnowledgeCandidate hit,
        ReviewerTicketContextAssembler.Bundle bundle)
    {
        var row = hit.Row;
        var phraseNeedles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            hit.MatchedPhrase.Trim(),
            row.Term.Trim(),
        };

        var ticketHit = Hits(bundle.TicketText, phraseNeedles);
        var externHit = Hits(bundle.ExternalBlob, phraseNeedles);
        var mappingHit = Hits(bundle.MappingBlob, phraseNeedles);

        var strengthLabel = hit.Strength switch
        {
            SynitiKnowledgeMatchStrength.Strong => "Strong catalog match",
            _ => "Phrase match",
        };

        var relatedPreview = FormatRelatedTermsPreview(row.RelatedTerms);

        var reviewerGuidanceText = FormatReviewerGuidance(row);

        var sourceReason = ComposeSourceReason(
            row,
            hit,
            ticketHit,
            externHit,
            mappingHit);

        var business = string.IsNullOrWhiteSpace(row.BusinessMeaning)
            ? null
            : row.BusinessMeaning.Trim();
        if (business is not null &&
            string.Equals(business, reviewerGuidanceText, StringComparison.Ordinal))
        {
            business = null;
        }

        return new SynitiKnowledgeContextMatchDto
        {
            Term = row.Term.Trim(),
            Category = row.Category.ToString(),
            ShortDefinition = row.ShortDefinition.Trim(),
            ReviewerGuidance = reviewerGuidanceText,
            BusinessMeaning = business,
            TechnicalMeaning = string.IsNullOrWhiteSpace(row.TechnicalMeaning)
                ? null
                : row.TechnicalMeaning.Trim(),
            RelatedTermsPreview = relatedPreview,
            SourceReason = sourceReason,
            MatchStrengthLabel = strengthLabel,
            SuggestedReviewerChecks = ParseDelimitedLines(row.SuggestedReviewerChecks),
            MissingContextQuestions = ParseDelimitedLines(row.MissingContextQuestions),
        };
    }

    private static string? FormatRelatedTermsPreview(string? relatedTerms)
    {
        if (string.IsNullOrWhiteSpace(relatedTerms))
        {
            return null;
        }

        var parts = relatedTerms
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(s => s.Length > 0)
            .Take(5)
            .ToList();

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string FormatReviewerGuidance(SynitiKnowledgeCatalogRow row)
    {
        if (!string.IsNullOrWhiteSpace(row.BusinessMeaning))
        {
            return row.BusinessMeaning.Trim();
        }

        return row.ShortDefinition.Trim();
    }

    private static IReadOnlyList<string> ParseDelimitedLines(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        var parts = raw
            .Split(new[] { '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static s => s.Trim())
            .Where(static s => s.Length > 0)
            .Take(12)
            .ToList();

        return parts;
    }

    private static bool Hits(string? segment, HashSet<string> needles)
    {
        if (string.IsNullOrWhiteSpace(segment))
        {
            return false;
        }

        var s = segment;
        foreach (var n in needles)
        {
            if (n.Length < 2)
            {
                continue;
            }

            if (s.Contains(n, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string ComposeSourceReason(
        SynitiKnowledgeCatalogRow row,
        SynitiKnowledgeCandidate hit,
        bool ticketHit,
        bool externHit,
        bool mappingHit)
    {
        var displayPhrase = hit.MatchedPhrase.Trim();
        var glossaryTerm = row.Term.Trim();

        string location;
        if (!ticketHit && !externHit && !mappingHit)
        {
            location = "the combined request and integration context";
        }
        else if (ticketHit && !externHit && !mappingHit)
        {
            location = "the request wording";
        }
        else
        {
            var bits = new List<string>();
            if (ticketHit)
            {
                bits.Add("the ticket");
            }

            if (externHit)
            {
                bits.Add("linked external item context");
            }

            if (mappingHit)
            {
                bits.Add("configured external field mappings");
            }

            var joined =
                bits.Count <= 2
                    ? string.Join(" and ", bits)
                    : string.Join(", ", bits[..^1]) + ", and " + bits[^1];
            location = joined;
        }

        var defPreview = Truncate(row.ShortDefinition.Trim(), 200);
        string via;
        if (hit.MatchedViaExamplePhrase)
        {
            via =
                $"This appears related to “{displayPhrase}”, which maps to the catalog term “{glossaryTerm}”. ";
        }
        else
        {
            via =
                $"The text references “{displayPhrase}”, aligned with “{glossaryTerm}” in the reference catalog. ";
        }

        var body =
            $"Review context from “{row.SourceName.Trim()}”: wording in {location} suggests this theme. {via}" +
            $"Summary: {defPreview} Stored reference catalog only — does not change routing, owners, or approvals.";

        return SpaceCollapse.Replace(body.Trim(), " ");
    }

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
