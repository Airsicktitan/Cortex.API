namespace Cortex.API.Models;

/// <summary>
/// Stores semantic embedding vectors for Cortex Memory v2 candidate retrieval.
/// The current UI and CortexInsightService remain keyword-based until vector search is wired in.
/// </summary>
public class TicketEmbedding
{
    public string TicketId { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = string.Empty;
    public string ContentHash { get; set; } = string.Empty;
    public string VectorJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
