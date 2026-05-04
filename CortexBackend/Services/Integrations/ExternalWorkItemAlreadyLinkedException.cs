namespace Cortex.API.Services.Integrations;

/// <summary>
/// The external item already has a <c>CortexTicketId</c>; duplicate ticket creation is not allowed.
/// </summary>
public sealed class ExternalWorkItemAlreadyLinkedException : Exception
{
    public string LinkedCortexTicketId { get; }

    public ExternalWorkItemAlreadyLinkedException(string linkedCortexTicketId)
        : base("This external item is already linked to a Cortex ticket.")
    {
        LinkedCortexTicketId = linkedCortexTicketId;
    }
}
