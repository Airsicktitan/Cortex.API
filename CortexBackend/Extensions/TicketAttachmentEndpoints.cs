using Cortex.API.Handlers;
using Cortex.API.DTO;
using Cortex.API.Models;

namespace Cortex.API.Extensions;

public static class TicketAttachmentEndpoints
{
    public static void MapTicketAttachmentEndpoints(this WebApplication app)
    {
        var attachments = app.MapGroup("/api/tickets/{ticketId}/attachments")
            .RequireAuthorization()
            .WithTags("Ticket Attachments");

        attachments.MapGet("/", TicketAttachmentHandlers.GetAttachments)
            .RequireAuthorization("TicketsRead")
            .WithName("GetTicketAttachments")
            .Produces<List<TicketAttachmentResponse>>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);

        attachments.MapPost("/", TicketAttachmentHandlers.UploadAttachments)
            .RequireAuthorization("TicketsWrite")
            .WithName("UploadTicketAttachments")
            .Accepts<IFormFileCollection>("multipart/form-data")
            .Produces<List<TicketAttachmentResponse>>(StatusCodes.Status201Created)
            .Produces(StatusCodes.Status400BadRequest)
            .Produces(StatusCodes.Status404NotFound);

        attachments.MapGet("/{attachmentId:int}/download", TicketAttachmentHandlers.DownloadAttachment)
            .RequireAuthorization("TicketsRead")
            .WithName("DownloadTicketAttachment")
            .Produces(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound);
    }
}
