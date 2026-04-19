namespace Cortex.API.DTO;

public sealed class RoutingPreviewResponse
{
    public TicketRoutingDecisionResponse Decision { get; set; } = null!;
}
