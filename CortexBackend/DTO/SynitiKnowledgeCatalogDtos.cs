namespace Cortex.API.DTO;

/// <summary>
/// Read-only catalog listing for Configuration visibility (advisory reference; not live integration).
/// </summary>
public sealed record SynitiKnowledgeCatalogListResponse(
    IReadOnlyList<SynitiKnowledgeCatalogEntryDto> Entries);

/// <summary>No internal database identifiers — trust and transparency UI only.</summary>
public sealed record SynitiKnowledgeCatalogEntryDto(
    string Term,
    string Category,
    string? Aliases,
    string? ExamplePhrases,
    string ShortDefinition,
    string? BusinessMeaning,
    string? TechnicalMeaning,
    IReadOnlyList<string> SuggestedReviewerChecks,
    IReadOnlyList<string> MissingContextQuestions,
    string? RelatedTerms,
    bool SourceIsEnabled,
    string SourceName,
    string SourceType,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);
