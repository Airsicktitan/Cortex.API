namespace Cortex.API.DTO;

public sealed class OwnerWorkloadPreviewRequest
{
    /// <summary>Stored owner tokens (Syniti/Business) to summarize. Duplicates are ignored.</summary>
    public List<string> OwnerKeys { get; set; } = [];

    /// <summary>Optional ticket id to exclude from counts (e.g. the ticket currently open).</summary>
    public string? ExcludeTicketId { get; set; }
}
