using Cortex.API.Models;

namespace Cortex.API.Services;

public interface ICortexEmbeddingService
{
    Task<TicketEmbedding?> EnsureEmbeddingAsync(
        string ticketId,
        CancellationToken cancellationToken = default);

    string BuildEmbeddingInputText(Ticket ticket);

    string ComputeContentHash(Ticket ticket);

    Task<bool> NeedsRegenerationAsync(
        Ticket ticket,
        string embeddingModel,
        CancellationToken cancellationToken = default);

    Task<TicketEmbedding> UpsertEmbeddingAsync(
        Ticket ticket,
        string embeddingModel,
        IReadOnlyList<float> vector,
        CancellationToken cancellationToken = default);
}
