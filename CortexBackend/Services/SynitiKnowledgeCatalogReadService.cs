using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public interface ISynitiKnowledgeCatalogReadService
{
    Task<SynitiKnowledgeCatalogListResponse> ListAsync(
        string? search,
        string? category,
        CancellationToken cancellationToken = default);
}

/// <summary>Read-only Syniti knowledge rows for admin/catalog visibility.</summary>
public sealed class SynitiKnowledgeCatalogReadService(CortexDbContext db) : ISynitiKnowledgeCatalogReadService
{
    public async Task<SynitiKnowledgeCatalogListResponse> ListAsync(
        string? search,
        string? category,
        CancellationToken cancellationToken = default)
    {
        var q = search ?? string.Empty;
        var normalizedSearch = q.Trim();
        SynitiKnowledgeCategory? categoryFilter = null;
        if (!string.IsNullOrWhiteSpace(category) &&
            Enum.TryParse(category.Trim(), ignoreCase: true, out SynitiKnowledgeCategory parsed))
        {
            categoryFilter = parsed;
        }

        var query = db.SynitiKnowledgeEntries.AsNoTracking()
            .Join(
                db.SynitiKnowledgeSources.AsNoTracking(),
                e => e.SynitiKnowledgeSourceId,
                s => s.Id,
                (e, s) => new { Entry = e, Source = s });

        if (categoryFilter is { } cf)
        {
            query = query.Where(x => x.Entry.Category == cf);
        }

        if (normalizedSearch.Length > 0)
        {
            var pattern = $"%{EscapeLikePattern(normalizedSearch)}%";
            query = query.Where(x =>
                EF.Functions.Like(x.Entry.Term, pattern) ||
                (x.Entry.BusinessMeaning != null && EF.Functions.Like(x.Entry.BusinessMeaning, pattern)) ||
                (x.Entry.ShortDefinition != null && EF.Functions.Like(x.Entry.ShortDefinition, pattern)) ||
                (x.Entry.Aliases != null && EF.Functions.Like(x.Entry.Aliases, pattern)) ||
                (x.Entry.ExamplePhrases != null && EF.Functions.Like(x.Entry.ExamplePhrases, pattern)) ||
                (x.Entry.RelatedTerms != null && EF.Functions.Like(x.Entry.RelatedTerms, pattern)) ||
                (x.Entry.SuggestedReviewerChecks != null &&
                    EF.Functions.Like(x.Entry.SuggestedReviewerChecks, pattern)) ||
                (x.Entry.MissingContextQuestions != null &&
                    EF.Functions.Like(x.Entry.MissingContextQuestions, pattern)));
        }

        var orderedQuery = normalizedSearch.Length > 0
            ? query.OrderBy(x => x.Entry.Id)
            : query.OrderBy(x => x.Entry.Term);

        var rawRows = await orderedQuery
            .Select(x => new
            {
                x.Entry.Term,
                x.Entry.Category,
                x.Entry.Aliases,
                x.Entry.ExamplePhrases,
                x.Entry.ShortDefinition,
                x.Entry.BusinessMeaning,
                x.Entry.TechnicalMeaning,
                x.Entry.SuggestedReviewerChecks,
                x.Entry.MissingContextQuestions,
                x.Entry.RelatedTerms,
                x.Source.IsEnabled,
                x.Source.Name,
                x.Source.SourceType,
                x.Entry.CreatedAtUtc,
                x.Entry.UpdatedAtUtc,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var rows = rawRows
            .Select(x => new SynitiKnowledgeCatalogEntryDto(
                x.Term.Trim(),
                x.Category.ToString(),
                string.IsNullOrWhiteSpace(x.Aliases) ? null : x.Aliases.Trim(),
                string.IsNullOrWhiteSpace(x.ExamplePhrases) ? null : x.ExamplePhrases.Trim(),
                x.ShortDefinition.Trim(),
                string.IsNullOrWhiteSpace(x.BusinessMeaning) ? null : x.BusinessMeaning.Trim(),
                string.IsNullOrWhiteSpace(x.TechnicalMeaning) ? null : x.TechnicalMeaning.Trim(),
                ParseDelimitedList(x.SuggestedReviewerChecks),
                ParseDelimitedList(x.MissingContextQuestions),
                string.IsNullOrWhiteSpace(x.RelatedTerms) ? null : x.RelatedTerms.Trim(),
                x.IsEnabled,
                x.Name.Trim(),
                x.SourceType.ToString(),
                x.CreatedAtUtc,
                x.UpdatedAtUtc))
            .ToList();

        if (normalizedSearch.Length > 0)
        {
            var qn = CatalogSearchRanking.NormalizeSearchText(normalizedSearch);
            rows = rows
                .OrderBy(e => CatalogSearchRanking.GetSynitiSortKey(e, qn))
                .ThenBy(e => e.Term, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        return new SynitiKnowledgeCatalogListResponse(rows);
    }

    private static string EscapeLikePattern(string value)
    {
        return value.Replace("[", "[[]", StringComparison.Ordinal)
            .Replace("%", "[%]", StringComparison.Ordinal)
            .Replace("_", "[_]", StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> ParseDelimitedList(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw
            .Split(new[] { '|', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(static s => s.Trim())
            .Where(static s => s.Length > 0)
            .Take(24)
            .ToList();
    }
}
