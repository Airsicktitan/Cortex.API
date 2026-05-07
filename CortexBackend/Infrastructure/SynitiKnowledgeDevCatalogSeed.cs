using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Infrastructure;

/// <summary>
/// Idempotently ensures a curated Syniti knowledge catalog exists (safe, generic entries only).
/// </summary>
public static class SynitiKnowledgeDevCatalogSeed
{
    private const string CuratedSourceName = "Cortex safe Syniti knowledge (curated v1)";

    public static async Task EnsureAsync(CortexDbContext db, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var source = await db.SynitiKnowledgeSources
            .FirstOrDefaultAsync(s => s.Name == CuratedSourceName, cancellationToken)
            .ConfigureAwait(false);

        if (source is null)
        {
            source = new SynitiKnowledgeSource
            {
                Name = CuratedSourceName,
                SourceType = SynitiKnowledgeSourceType.Manual,
                Version = "v1",
                IsEnabled = true,
                CreatedAtUtc = now,
            };
            db.SynitiKnowledgeSources.Add(source);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var termList = await db.SynitiKnowledgeEntries
            .Select(e => e.Term)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var takenTerms = termList.ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var def in SynitiKnowledgeCuratedCatalog.CuratedEntries)
        {
            if (takenTerms.Contains(def.Term))
            {
                continue;
            }

            db.SynitiKnowledgeEntries.Add(new SynitiKnowledgeEntry
            {
                SynitiKnowledgeSourceId = source.Id,
                Term = def.Term,
                Category = def.Category,
                ShortDefinition = def.ShortDefinition,
                BusinessMeaning = def.ReviewerGuidance,
                TechnicalMeaning = null,
                CommonSignals = null,
                RelatedTerms = def.RelatedTerms,
                ExamplePhrases = def.ExamplePhrases,
                Aliases = def.Aliases,
                SuggestedReviewerChecks = def.SuggestedReviewerChecks,
                MissingContextQuestions = def.MissingContextQuestions,
                CreatedAtUtc = now,
            });

            takenTerms.Add(def.Term);
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
