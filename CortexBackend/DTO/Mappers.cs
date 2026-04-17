using Cortex.API.Models;
using Cortex.API.Services;

namespace Cortex.API.DTO;

public static class UserResponseExtensions
{
    public static UserResponse ToResponse(this User user, IReadOnlyList<string>? auth0Roles = null)
    {
        var roles = auth0Roles is { Count: > 0 }
            ? auth0Roles.ToList()
            : new List<string> { user.Role };

        return new UserResponse
        {
            Id = user.Id,
            DisplayName = user.DisplayName ?? string.Empty,
            NickName = user.NickName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            Department = user.Department ?? string.Empty,
            AssignmentNotificationChannel = user.AssignmentNotificationChannel?.ToString(),
            SlaRiskNotificationChannel = user.SlaRiskNotificationChannel?.ToString(),
            Role = user.Role,
            Roles = roles,
            IsActive = user.IsActive,
            CreatedDate = user.CreatedDate,
            LastLoginDate = user.LastLoginDate,
            LastSeenDateUtc = user.LastSeenDateUtc,
            ExpiryDate = user.ExpiryDate,
            LastModifiedDate = user.LastModifiedDate,
        };
    }

    public static AdminUserResponse ToAdminResponse(
        this User user,
        IReadOnlyList<string>? auth0RoleNames = null)
    {
        var roles = auth0RoleNames is { Count: > 0 }
            ? auth0RoleNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList()
            : new List<string>();

        return new AdminUserResponse
        {
            Id = user.Id,
            DisplayName = user.DisplayName ?? string.Empty,
            NickName = user.NickName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            Department = user.Department ?? string.Empty,
            AssignmentNotificationChannel = user.AssignmentNotificationChannel?.ToString(),
            SlaRiskNotificationChannel = user.SlaRiskNotificationChannel?.ToString(),
            Role = user.Role,
            Roles = roles,
            CreatedDate = user.CreatedDate,
            LastLoginDate = user.LastLoginDate,
            LastSeenDateUtc = user.LastSeenDateUtc,
            ExpiryDate = user.ExpiryDate,
            IsActive = user.IsActive,
            Auth0Id = user.Auth0Id,
            LastModifiedDate = user.LastModifiedDate
        };
    }

    public static OnlineUserResponse ToOnlineResponse(this User user)
    {
        return new OnlineUserResponse
        {
            Id = user.Id,
            DisplayName = user.DisplayName ?? string.Empty,
            NickName = user.NickName,
            Email = user.Email ?? string.Empty,
            Department = user.Department ?? string.Empty,
            Role = user.Role,
            LastSeenDateUtc = user.LastSeenDateUtc,
            LastLoginDate = user.LastLoginDate
        };
    }
}

public static class CommentMappings
{
    public static CommentResponse ToResponse(
        this Comment comment,
        ResponseMappingContext? mappingContext = null)
    {
        var context = mappingContext ?? ResponseMappingContext.Empty;

        return new CommentResponse
        {
            Id = comment.Id,
            TicketId = comment.TicketId,
            Body = comment.Body,
            CreatedBy = comment.CreatedBy,
            CreatedByDisplayName = context.ResolveUserDisplayName(
                comment.CreatedBy,
                comment.CreatedByUser),
            CreatedDate = comment.CreatedDate,
            LastModifiedDate = comment.LastModifiedDate
        };
    }
}

public static class TicketResponseExtensions
{
    public static TicketResponse ToResponse(
        this Ticket ticket,
        IReadOnlyDictionary<string, SlaConfiguration> slaConfigurations,
        ResponseMappingContext? mappingContext = null)
    {
        var context = mappingContext ?? ResponseMappingContext.Empty;
        slaConfigurations.TryGetValue(ticket.Priority, out var configuration);
        var slaSnapshot = TicketSlaCalculator.Calculate(ticket, configuration);

        return new TicketResponse
        {
            Id = ticket.Id,
            Title = ticket.Title,
            Description = ticket.Description,
            Status = ticket.Status,
            Priority = ticket.Priority,
            BoardId = ticket.BoardId,
            BoardName = context.ResolveBoardName(ticket.BoardId, ticket.BoardDefinition) ?? string.Empty,
            StoryPoints = ticket.StoryPoints,
            SynitiOwner = ticket.SynitiOwner,
            BusinessOwner = ticket.BusinessOwner,
            CreatedBy = ticket.CreatedBy,
            CreatedDate = ticket.CreatedDate,
            LastModifiedBy = ticket.LastModifiedBy,
            LastModifiedDate = ticket.LastModifiedDate,
            CreatedByDisplayName = context.ResolveUserDisplayName(
                ticket.CreatedBy,
                ticket.CreatedByUser),
            SlaTargetDate = slaSnapshot.TargetDateUtc,
            SlaCompletedDate = slaSnapshot.CompletedDateUtc,
            SlaStatus = slaSnapshot.Status,
            SlaRemainingMinutes = slaSnapshot.RemainingMinutes,
            IsSlaBreached = slaSnapshot.IsBreached,
            ConcurrencyToken = ticket.RowVersion is { Length: > 0 }
                ? Convert.ToBase64String(ticket.RowVersion)
                : string.Empty
        };
    }
}

public static class TicketAuditMappings
{
    public static TicketAuditEntryResponse ToResponse(
        this TicketAuditEntry entry,
        ResponseMappingContext? mappingContext = null)
    {
        var context = mappingContext ?? ResponseMappingContext.Empty;

        return new TicketAuditEntryResponse
        {
            Id = entry.Id,
            TicketId = entry.TicketId,
            Action = entry.Action,
            Summary = entry.Summary,
            Reason = entry.Reason,
            ChangedBy = entry.ChangedBy,
            ChangedByDisplayName = context.ResolveUserDisplayName(
                entry.ChangedBy,
                entry.ChangedByUser),
            ChangedDateUtc = entry.ChangedDateUtc,
            FieldChanges = entry.FieldChanges
                .OrderBy(change => change.Id)
                .Select(change => new TicketAuditFieldChangeResponse
                {
                    FieldName = change.FieldName,
                    OldValue = change.OldValue,
                    NewValue = change.NewValue
                })
                .ToList()
        };
    }
}

public static class ArchivedTicketMappings
{
    public static ArchivedTicketResponse ToResponse(
        this ArchivedTicket ticket,
        ResponseMappingContext? mappingContext = null)
    {
        var context = mappingContext ?? ResponseMappingContext.Empty;

        return new ArchivedTicketResponse
        {
            Id = ticket.Id,
            Title = ticket.Title,
            Description = ticket.Description,
            Status = ticket.Status,
            Priority = ticket.Priority,
            BoardId = ticket.BoardId,
            BoardName = context.ResolveBoardName(ticket.BoardId, ticket.BoardDefinition) ?? string.Empty,
            StoryPoints = ticket.StoryPoints,
            SynitiOwner = ticket.SynitiOwner,
            BusinessOwner = ticket.BusinessOwner,
            CreatedBy = ticket.CreatedBy,
            CreatedByDisplayName = context.ResolveUserDisplayName(
                ticket.CreatedBy,
                ticket.CreatedByUser),
            CreatedDate = ticket.CreatedDate,
            LastModifiedBy = ticket.LastModifiedBy,
            LastModifiedDate = ticket.LastModifiedDate,
            ArchivedBy = ticket.ArchivedBy,
            ArchivedByDisplayName = context.ResolveUserDisplayName(
                ticket.ArchivedBy,
                ticket.ArchivedByUser),
            ArchivedDate = ticket.ArchivedDate,
            CommentCount = ticket.CommentCount,
            AttachmentCount = ticket.AttachmentCount
        };
    }
}

public static class TicketBoardDefinitionMappings
{
    public static TicketBoardDefinitionResponse ToResponse(this TicketBoardDefinition definition)
    {
        return new TicketBoardDefinitionResponse
        {
            Id = definition.Id,
            Name = definition.Name,
            Description = definition.Description,
            RequiresStoryPoints = definition.RequiresStoryPoints,
            IsEnabled = definition.IsEnabled,
            CreatedDateUtc = definition.CreatedDateUtc,
            LastModifiedDateUtc = definition.LastModifiedDateUtc
        };
    }
}

public static class TicketAttachmentMappings
{
    public static TicketAttachmentResponse ToResponse(
        this TicketAttachment attachment,
        ResponseMappingContext? mappingContext = null)
    {
        var context = mappingContext ?? ResponseMappingContext.Empty;

        return new TicketAttachmentResponse
        {
            Id = attachment.Id,
            TicketId = attachment.TicketId,
            FileName = attachment.FileName,
            ContentType = attachment.ContentType,
            FileSize = attachment.FileSize,
            UploadedBy = attachment.UploadedBy,
            UploadedByDisplayName = context.ResolveUserDisplayName(
                attachment.UploadedBy,
                attachment.UploadedByUser),
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
            Id = configuration.Id,
            ArchiveAfterDays = configuration.ArchiveAfterDays,
            EligibleStatuses = [.. configuration.EligibleStatuses]
        };
    }
}

public static class TicketRoutingRuleMappings
{
    public static TicketRoutingRuleResponse ToResponse(this TicketRoutingRule rule)
    {
        return new TicketRoutingRuleResponse
        {
            Id = rule.Id,
            Department = rule.Department ?? string.Empty,
            TitleContains = rule.TitleContains ?? string.Empty,
            SynitiOwner = rule.SynitiOwner ?? string.Empty,
            BusinessOwner = rule.BusinessOwner ?? string.Empty,
            IsEnabled = rule.IsEnabled,
            CreatedDateUtc = rule.CreatedDateUtc,
            LastModifiedDateUtc = rule.LastModifiedDateUtc
        };
    }
}

public static class TicketStatusDefinitionMappings
{
    public static TicketStatusDefinitionResponse ToResponse(this TicketStatusDefinition definition)
    {
        return new TicketStatusDefinitionResponse
        {
            Id = definition.Id,
            Name = definition.Name,
            Description = definition.Description,
            IsEnabled = definition.IsEnabled,
            CreatedDateUtc = definition.CreatedDateUtc.ToString("O"),
            LastModifiedDateUtc = definition.LastModifiedDateUtc?.ToString("O")
        };
    }
}

public static class SessionConfigurationMappings
{
    public static SessionConfigurationResponse ToResponse(this SessionConfiguration configuration)
    {
        return new SessionConfigurationResponse
        {
            InactivityTimeoutMinutes = configuration.InactivityTimeoutMinutes,
            WarningMinutes = configuration.WarningMinutes
        };
    }
}

public static class NotificationChannelConfigurationMappings
{
    public static NotificationChannelConfigurationResponse ToResponse(
        this NotificationChannelConfiguration configuration)
    {
        return new NotificationChannelConfigurationResponse
        {
            AssignmentChannel = configuration.AssignmentChannel.ToString(),
            SlaRiskChannel = configuration.SlaRiskChannel.ToString()
        };
    }
}

public static class ReportDefinitionMappings
{
    public static ReportDefinitionResponse ToResponse(this ReportDefinition definition)
    {
        return new ReportDefinitionResponse
        {
            Id = definition.Id,
            Name = definition.Name,
            ViewName = definition.ViewName,
            Description = definition.Description,
            SqlQuery = definition.SqlQuery,
            IsEnabled = definition.IsEnabled,
            CreatedDateUtc = definition.CreatedDateUtc,
            LastModifiedDateUtc = definition.LastModifiedDateUtc
        };
    }
}

public static class StoredProcedureDefinitionMappings
{
    public static StoredProcedureDefinitionResponse ToResponse(this StoredProcedureDefinition definition)
    {
        return new StoredProcedureDefinitionResponse
        {
            Id = definition.Id,
            Name = definition.Name,
            ProcedureName = definition.ProcedureName,
            DefinitionSql = definition.DefinitionSql,
            Description = definition.Description,
            IsEnabled = definition.IsEnabled,
            CreatedDateUtc = definition.CreatedDateUtc,
            LastModifiedDateUtc = definition.LastModifiedDateUtc
        };
    }
}

public static class DatabaseViewDefinitionMappings
{
    public static DatabaseViewDefinitionResponse ToResponse(this DatabaseViewDefinition definition)
    {
        return new DatabaseViewDefinitionResponse
        {
            ViewName = definition.ViewName,
            DefinitionSql = definition.DefinitionSql
        };
    }
}

public static class DatabaseStoredProcedureDefinitionMappings
{
    public static DatabaseStoredProcedureDefinitionResponse ToResponse(this DatabaseStoredProcedureDefinition definition)
    {
        return new DatabaseStoredProcedureDefinitionResponse
        {
            ProcedureName = definition.ProcedureName,
            DefinitionSql = definition.DefinitionSql
        };
    }
}

public static class ScheduledJobMappings
{
    public static ScheduledJobResponse ToResponse(
        this ScheduledJob job,
        ResponseMappingContext? mappingContext = null)
    {
        var context = mappingContext ?? ResponseMappingContext.Empty;

        return new ScheduledJobResponse
        {
            Id = job.Id,
            Name = job.Name,
            Description = job.Description,
            JobType = job.JobType.ToString(),
            IntervalMinutes = job.IntervalMinutes,
            IsEnabled = job.IsEnabled,
            StoredProcedureDefinitionId = job.StoredProcedureDefinitionId,
            StoredProcedureName = context.ResolveStoredProcedureLabel(
                job.StoredProcedureDefinitionId,
                job.StoredProcedureDefinition),
            RunAsUserId = job.RunAsUserId,
            RunAsDisplayName = context.ResolveUserDisplayName(
                job.RunAsUserId,
                job.RunAsUser),
            CreatedDateUtc = job.CreatedDateUtc,
            LastModifiedDateUtc = job.LastModifiedDateUtc,
            LastRunDateUtc = job.LastRunDateUtc,
            NextRunDateUtc = job.NextRunDateUtc,
            LastRunStatus = job.LastRunStatus,
            LastRunMessage = job.LastRunMessage
        };
    }
}
