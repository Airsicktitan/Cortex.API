namespace Cortex.API.DTO;

/// <summary>GET /api/tickets/{ticketId}/syniti-knowledge-context — advisory Syniti/DSP terminology (no raw IDs).</summary>
public sealed record SynitiKnowledgeContextDto(
    string TicketId,
    IReadOnlyList<SynitiKnowledgeContextMatchDto> Matches);

public sealed record SynitiKnowledgeContextMatchDto
{
    public required string Term { get; init; }

    public required string Category { get; init; }

    public required string ShortDefinition { get; init; }

    public string? BusinessMeaning { get; init; }

    public string? TechnicalMeaning { get; init; }

    public string? RelatedTermsPreview { get; init; }

    public required string SourceReason { get; init; }

    public required string MatchStrengthLabel { get; init; }
}
