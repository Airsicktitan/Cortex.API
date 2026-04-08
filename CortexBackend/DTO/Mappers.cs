using Cortex.API.Models;
using Cortex.API.Services;

namespace Cortex.API.DTO;


public static class UserResponseExtensions
{
    public static UserResponse ToResponse(this User user)
    {
        return new UserResponse
        {
            Id = user.Id,
            DisplayName = user.DisplayName ?? string.Empty,
            NickName = user.NickName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            Department = user.Department ?? string.Empty,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            CreatedDate = user.CreatedDate,
            LastLoginDate = user.LastLoginDate,
            ExpiryDate = user.ExpiryDate,
            LastModifiedDate = user.LastModifiedDate,
        };
    }

    public static AdminUserResponse ToAdminResponse(this User user)
    {
        return new AdminUserResponse
        {
            Id = user.Id,
            DisplayName = user.DisplayName ?? string.Empty,
            NickName = user.NickName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            Department = user.Department ?? string.Empty,
            Role = user.Role.ToString(),
            CreatedDate = user.CreatedDate,
            LastLoginDate = user.LastLoginDate,
            ExpiryDate = user.ExpiryDate,
            IsActive = user.IsActive,
            Auth0Id = user.Auth0Id,
            LastModifiedDate = user.LastModifiedDate
        };
    }
}

public static class CommentMappings
{
    public static CommentResponse ToResponse(this Comment comment)
    {
        return new CommentResponse
        {
            Id = comment.Id,
            TicketId = comment.TicketId,
            Body = comment.Body,
            CreatedBy = comment.CreatedBy,
            CreatedByDisplayName = comment.CreatedByUser?.DisplayName ?? "Unknown User",
            CreatedDate = comment.CreatedDate,
            LastModifiedDate = comment.LastModifiedDate
        };
    }
}

public static class TicketResponseExtensions
{
    public static TicketResponse ToResponse(
        this Ticket ticket,
        IReadOnlyDictionary<string, SlaConfiguration> slaConfigurations)
    {
        slaConfigurations.TryGetValue(ticket.Priority, out var configuration);
        var slaSnapshot = TicketSlaCalculator.Calculate(ticket, configuration);

        return new TicketResponse
        {
            Id = ticket.Id,
            Title = ticket.Title,
            Description = ticket.Description,
            Status = ticket.Status,
            Priority = ticket.Priority,
            SynitiOwner = ticket.SynitiOwner,
            BusinessOwner = ticket.BusinessOwner,
            CreatedBy = ticket.CreatedBy,
            CreatedDate = ticket.CreatedDate,
            LastModifiedBy = ticket.LastModifiedBy,
            LastModifiedDate = ticket.LastModifiedDate,
            CreatedByDisplayName = ticket.CreatedByUser?.DisplayName ?? "Unknown User",
            SlaTargetDate = slaSnapshot.TargetDateUtc,
            SlaCompletedDate = slaSnapshot.CompletedDateUtc,
            SlaStatus = slaSnapshot.Status,
            SlaRemainingMinutes = slaSnapshot.RemainingMinutes,
            IsSlaBreached = slaSnapshot.IsBreached
        };
    }
}

public static class ArchivedTicketMappings
{
    public static ArchivedTicketResponse ToResponse(this ArchivedTicket ticket)
    {
        return new ArchivedTicketResponse
        {
            Id = ticket.Id,
            Title = ticket.Title,
            Description = ticket.Description,
            Status = ticket.Status,
            Priority = ticket.Priority,
            SynitiOwner = ticket.SynitiOwner,
            BusinessOwner = ticket.BusinessOwner,
            CreatedBy = ticket.CreatedBy,
            CreatedByDisplayName = ticket.CreatedByUser?.DisplayName ?? "Unknown User",
            CreatedDate = ticket.CreatedDate,
            LastModifiedBy = ticket.LastModifiedBy,
            LastModifiedDate = ticket.LastModifiedDate,
            ArchivedBy = ticket.ArchivedBy,
            ArchivedByDisplayName = ticket.ArchivedByUser?.DisplayName ?? "Unknown User",
            ArchivedDate = ticket.ArchivedDate,
            CommentCount = ticket.CommentCount,
            AttachmentCount = ticket.AttachmentCount
        };
    }
}

public static class TicketAttachmentMappings
{
    public static TicketAttachmentResponse ToResponse(this TicketAttachment attachment)
    {
        return new TicketAttachmentResponse
        {
            Id = attachment.Id,
            TicketId = attachment.TicketId,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            FileSize = attachment.FileSize,
            UploadedBy = attachment.UploadedBy,
            UploadedByDisplayName = attachment.UploadedByUser?.DisplayName ?? "Unknown User",
            UploadedDate = attachment.UploadedDate
        };
    }
}

public static class SlaConfigurationMappings
{
    public static SlaConfigurationResponse ToResponse(this SlaConfiguration configuration)
    {
        return new SlaConfigurationResponse
        {
            Priority = configuration.Priority,
            TargetHours = configuration.TargetHours,
            WarningHours = configuration.WarningHours
        };
    }
}

public static class ArchiveConfigurationMappings
{
    public static ArchiveConfigurationResponse ToResponse(this ArchiveConfiguration configuration)
    {
        return new ArchiveConfigurationResponse
        {
            ArchiveAfterDays = configuration.ArchiveAfterDays,
            ArchiveResolvedTickets = configuration.ArchiveResolvedTickets,
            ArchiveClosedTickets = configuration.ArchiveClosedTickets
        };
    }
}
