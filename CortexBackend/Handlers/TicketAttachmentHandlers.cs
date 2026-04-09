using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;

namespace Cortex.API.Handlers;

public static class TicketAttachmentHandlers
{
    private const long MaxAttachmentBytes = 10 * 1024 * 1024;
    private const int MaxFilesPerUpload = 10;

    public static async Task<IResult> GetAttachments(
        string ticketId,
        ITicketRepository ticketRepository,
        ITicketAttachmentRepository attachmentRepository,
        ITicketVisibilityService ticketVisibilityService,
        IResponseMappingContextFactory mappingContextFactory)
    {
        var ticket = await ticketRepository.GetTicketByIdAsync(ticketId);
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        var attachments = (await attachmentRepository.GetByTicketIdAsync(ticketId)).ToList();
        var mappingContext = await mappingContextFactory.CreateAsync(
            attachments.Select(attachment => attachment.UploadedBy));
        return Results.Ok(attachments.Select(attachment => attachment.ToResponse(mappingContext)));
    }

    public static async Task<IResult> UploadAttachments(
        string ticketId,
        HttpRequest request,
        ITicketRepository ticketRepository,
        ITicketAttachmentRepository attachmentRepository,
        ITicketVisibilityService ticketVisibilityService,
        IUserContextService userContext,
        ITicketAuditService ticketAuditService,
        IRealtimeEventService realtimeEventService,
        IResponseMappingContextFactory mappingContextFactory)
    {
        var ticket = await ticketRepository.GetTicketByIdAsync(ticketId);
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        if (!request.HasFormContentType)
        {
            return Results.BadRequest("Attachments must be uploaded as multipart form data.");
        }

        var form = await request.ReadFormAsync();
        if (form.Files.Count == 0)
        {
            return Results.BadRequest("Select at least one attachment to upload.");
        }

        if (form.Files.Count > MaxFilesPerUpload)
        {
            return Results.BadRequest($"You can upload up to {MaxFilesPerUpload} attachments at a time.");
        }

        var currentUser = await userContext.GetCurrentUserAsync();
        var attachments = new List<TicketAttachment>();

        foreach (var file in form.Files)
        {
            if (file.Length == 0)
            {
                return Results.BadRequest($"'{file.FileName}' is empty.");
            }

            if (file.Length > MaxAttachmentBytes)
            {
                return Results.BadRequest($"'{file.FileName}' exceeds the 10 MB attachment limit.");
            }

            await using var stream = file.OpenReadStream();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);

            attachments.Add(new TicketAttachment
            {
                TicketId = ticketId,
                FileName = Path.GetFileName(file.FileName),
                ContentType = string.IsNullOrWhiteSpace(file.ContentType)
                    ? "application/octet-stream"
                    : file.ContentType,
                FileSize = file.Length,
                Content = memoryStream.ToArray(),
                UploadedBy = currentUser.Id,
                UploadedDate = DateTime.UtcNow,
                UploadedByUser = currentUser
            });
        }

        await attachmentRepository.AddRangeAsync(attachments);
        await attachmentRepository.SaveChangesAsync();
        await ticketAuditService.RecordAttachmentsAddedAsync(
            ticketId,
            attachments,
            currentUser);
        await realtimeEventService.PublishAsync(new RealtimeEventMessage
        {
            EventType = "attachment.created",
            TicketId = ticketId,
            EntityId = string.Join(",", attachments.Select(attachment => attachment.Id))
        });

        var mappingContext = await mappingContextFactory.CreateAsync(
            attachments.Select(attachment => attachment.UploadedBy));

        return Results.Created(
            $"/api/tickets/{ticketId}/attachments",
            attachments.Select(attachment => attachment.ToResponse(mappingContext)));
    }

    public static async Task<IResult> DownloadAttachment(
        string ticketId,
        int attachmentId,
        ITicketRepository ticketRepository,
        ITicketAttachmentRepository attachmentRepository,
        ITicketVisibilityService ticketVisibilityService)
    {
        var ticket = await ticketRepository.GetTicketByIdAsync(ticketId);
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        var attachment = await attachmentRepository.GetByIdAsync(attachmentId);
        if (attachment is null || !string.Equals(attachment.TicketId, ticketId, StringComparison.OrdinalIgnoreCase))
        {
            return Results.NotFound();
        }

        return Results.File(
            attachment.Content,
            attachment.ContentType,
            attachment.FileName,
            enableRangeProcessing: true);
    }
}
