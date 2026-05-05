using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Infrastructure;

/// <summary>Development-only Syniti/DSP glossary rows when the catalog is empty.</summary>
public static class SynitiKnowledgeDevCatalogSeed
{
    public static async Task EnsureAsync(CortexDbContext db, CancellationToken cancellationToken = default)
    {
        if (await db.SynitiKnowledgeSources.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = DateTime.UtcNow;
        var src = new SynitiKnowledgeSource
        {
            Name = "Demo Syniti knowledge (local)",
            SourceType = SynitiKnowledgeSourceType.Manual,
            Version = "dev-seed",
            IsEnabled = true,
            CreatedAtUtc = now,
        };
        db.SynitiKnowledgeSources.Add(src);
        await db.SaveChangesAsync(cancellationToken);

        void Entry(
            string term,
            SynitiKnowledgeCategory category,
            string shortDef,
            string? businessMeaning,
            string? related,
            string? examples)
        {
            db.SynitiKnowledgeEntries.Add(new SynitiKnowledgeEntry
            {
                SynitiKnowledgeSourceId = src.Id,
                Term = term,
                Category = category,
                ShortDefinition = shortDef,
                BusinessMeaning = businessMeaning,
                TechnicalMeaning = null,
                CommonSignals = null,
                RelatedTerms = related,
                ExamplePhrases = examples,
                CreatedAtUtc = now,
            });
        }

        Entry(
            "DSP",
            SynitiKnowledgeCategory.Platform,
            "Syniti’s data migration and governance platform used to manage migration, validation, mapping, construction, and related data processes.",
            "Typically refers to orchestrating repeatable migration waves, validations, mappings, and construction activities with reviewer oversight.",
            "ADM; Governance; Waves",
            "Syniti DSP; data governance platform");

        Entry(
            "ADM",
            SynitiKnowledgeCategory.Platform,
            "Advanced Data Migration context used to support structured migration execution and governance.",
            null,
            "DSP; Waves; Scenarios",
            "advanced data migration; ADMM");

        Entry(
            "Value Mapping",
            SynitiKnowledgeCategory.Mapping,
            "A mapping process that translates source values into target values using controlled business rules or lookup references.",
            null,
            "Lookup tables; Transform rules",
            "value mappings; translate source values to target");

        Entry(
            "Data Quality Rule",
            SynitiKnowledgeCategory.DataQuality,
            "A validation rule used to identify whether source or target data meets expected business requirements.",
            null,
            "Validation; DQ checks",
            "data quality rules; dq rule");

        Entry(
            "Wave",
            SynitiKnowledgeCategory.Migration,
            "A planned migration or rollout grouping used to organize scope, timing, objects, and execution.",
            null,
            "Scenario; Cutover",
            "migration wave; rollout wave");

        await db.SaveChangesAsync(cancellationToken);
    }
}
