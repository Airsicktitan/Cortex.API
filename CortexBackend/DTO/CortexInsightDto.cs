namespace Cortex.API.DTO;

using System.Text.Json.Serialization;

/// <summary>
/// Advisory memory insight for a ticket, grounded in similar prior tickets.
/// The generated fields are never persisted and must not drive automatic ownership changes.
/// </summary>
public sealed class CortexInsightDto
{
    public string TicketId { get; set; } = string.Empty;
    public List<CortexInsightSimilarTicketDto> Matches { get; set; } = [];
    public int ConfidenceScore { get; set; }
    public List<string> MatchReasons { get; set; } = [];
    public string? Summary { get; set; }
    public string? Resolution { get; set; }
    public string? RootCause { get; set; }
    public string? SuggestedNextStep { get; set; }
    public bool Unavailable { get; set; }
    public string? UnavailableReason { get; set; }

    /// <summary>
    /// Advisory learning signals derived from prior ticket outcomes.
    /// Empty list when there is insufficient outcome history.
    /// </summary>
    public List<CortexLearningSignalDto> LearningSignals { get; set; } = [];

    [JsonIgnore]
    public List<CortexInsightSimilarTicketDto> SimilarTickets
    {
        get => Matches;
        set => Matches = value;
    }
}

public sealed class CortexInsightSimilarTicketDto
{
    public string Id { get; set; } = string.Empty;
    public string SourceTicketId { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? LastMeaningfulComment { get; set; }
    public string? SourceQuote { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public int SimilarityScore { get; set; }
    public int ConfidenceScore { get; set; }
    public List<string> MatchReasons { get; set; } = [];
}
