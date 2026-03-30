using Cortex.API.Models;

namespace Cortex.API.DTO;


public static class UserResponseExtensions
{
    public static UserResponse ToResponse(this User user)
    {
        return new UserResponse
        {
            Id = user.Id,
            DisplayName = user.DisplayName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Department = user.Department ?? string.Empty,
            Role = user.Role.ToString(),
            IsActive = user.IsActive,
            CreatedDate = user.CreatedDate,
            LastLoginDate = user.LastLoginDate,
            LastModifiedDate = user.LastModifiedDate,
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
    public static TicketResponse ToResponse(this Ticket ticket)
    {
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
            CreatedByDisplayName = ticket.CreatedByUser?.DisplayName ?? "Unknown User"
        };
    }
}