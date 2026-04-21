using System.Text.Json;
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

    public static UserDirectoryEntryResponse ToDirectoryResponse(this User user)
    {
        return new UserDirectoryEntryResponse
        {
            Id = user.Id,
            DisplayName = string.IsNullOrWhiteSpace(user.DisplayName)
                ? user.Email ?? string.Empty
                : user.DisplayName.Trim(),
            Email = user.Email ?? string.Empty,
            Department = string.IsNullOrWhiteSpace(user.Department)
                ? null
                : user.Department.Trim(),
            Role = string.IsNullOrWhiteSpace(user.Role)
                ? null
                : user.Role.Trim()
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

    public static CommentResponse ToResponse(
        this ArchivedComment comment,
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
    private static readonly JsonSerializerOptions ScreenshotInsightJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

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
            ApprovalStatus = ticket.ApprovalStatus,
            Priority = ticket.Priority,
            BoardId = ticket.BoardId,
            BoardName = context.ResolveBoardName(ticket.BoardId, ticket.BoardDefinition) ?? string.Empty,
            StoryPoints = ticket.StoryPoints,
            SynitiOwner = ticket.SynitiOwner,
            BusinessOwner = ticket.BusinessOwner,
            SynitiOwnerDisplayName = string.IsNullOrWhiteSpace(ticket.SynitiOwner)
                ? null
                : context.ResolveOwnerFieldDisplayName(ticket.SynitiOwner),
            BusinessOwnerDisplayName = string.IsNullOrWhiteSpace(ticket.BusinessOwner)
                ? null
                : context.ResolveOwnerFieldDisplayName(ticket.BusinessOwner),
            CreatedBy = ticket.CreatedBy,
            CreatedDate = ticket.CreatedDate,
            LastModifiedBy = ticket.LastModifiedBy,
            LastModifiedDate = ticket.LastModifiedDate,
            CreatedByDisplayName = context.ResolveUserDisplayName(
                ticket.CreatedBy,
                ticket.CreatedByUser),
            CreatedByEmail = context.ResolveUserEmail(
                ticket.CreatedBy,
                ticket.CreatedByUser),
            CreatedByAuth0Id = context.ResolveUserAuth0Id(
                ticket.CreatedBy,
                ticket.CreatedByUser),
            ApprovedAt = ticket.ApprovedAt,
            ApprovedBy = ticket.ApprovedBy,
            RejectedAt = ticket.RejectedAt,
            RejectedBy = ticket.RejectedBy,
            RejectionReason = ticket.RejectionReason,
            ReturnedForDetailAt = ticket.ReturnedForDetailAt,
            ReturnedForDetailBy = ticket.ReturnedForDetailBy,
            ReturnReason = ticket.ReturnReason,
            ApprovalTriagePreview = MapApprovalTriagePreview(ticket),
            ScreenshotInsight = MapScreenshotInsightPersisted(ticket),
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

    private static ApprovalTriagePreviewDto? MapApprovalTriagePreview(Ticket ticket)
    {
        List<string> hints = [];
        if (!string.IsNullOrWhiteSpace(ticket.AiTriageMissingDetailsJson))
        {
            try
            {
                hints = JsonSerializer.Deserialize<List<string>>(ticket.AiTriageMissingDetailsJson) ?? [];
            }
            catch (JsonException)
            {
                hints = [];
            }
        }

        var hasAny =
            !string.IsNullOrWhiteSpace(ticket.AiTriageSummary)
            || !string.IsNullOrWhiteSpace(ticket.AiTriageSuggestedPriority)
            || !string.IsNullOrWhiteSpace(ticket.AiTriagePriorityReason)
            || !string.IsNullOrWhiteSpace(ticket.AiTriageSuggestedStatus)
            || !string.IsNullOrWhiteSpace(ticket.AiTriagePotentialSlaRisk)
            || !string.IsNullOrWhiteSpace(ticket.AiTriageSlaRiskReason)
            || hints.Count > 0;

        if (!hasAny)
        {
            return null;
        }

        return new ApprovalTriagePreviewDto
        {
            Summary = string.IsNullOrWhiteSpace(ticket.AiTriageSummary)
                ? null
                : ticket.AiTriageSummary.Trim(),
            SuggestedPriority = string.IsNullOrWhiteSpace(ticket.AiTriageSuggestedPriority)
                ? null
                : ticket.AiTriageSuggestedPriority.Trim(),
            PriorityReason = string.IsNullOrWhiteSpace(ticket.AiTriagePriorityReason)
                ? null
                : ticket.AiTriagePriorityReason.Trim(),
            SuggestedStatus = string.IsNullOrWhiteSpace(ticket.AiTriageSuggestedStatus)
                ? null
                : ticket.AiTriageSuggestedStatus.Trim(),
            MissingDetailHints = hints,
            PotentialSlaRisk = string.IsNullOrWhiteSpace(ticket.AiTriagePotentialSlaRisk)
                ? null
                : ticket.AiTriagePotentialSlaRisk.Trim(),
            SlaRiskReason = string.IsNullOrWhiteSpace(ticket.AiTriageSlaRiskReason)
                ? null
                : ticket.AiTriageSlaRiskReason.Trim(),
        };
    }

    private static ScreenshotInsightPersistedDto? MapScreenshotInsightPersisted(Ticket ticket)
    {
        if (string.IsNullOrWhiteSpace(ticket.AiScreenshotInsightJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ScreenshotInsightPersistedDto>(
                ticket.AiScreenshotInsightJson,
                ScreenshotInsightJsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
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
            SynitiOwnerDisplayName = string.IsNullOrWhiteSpace(ticket.SynitiOwner)
                ? null
                : context.ResolveOwnerFieldDisplayName(ticket.SynitiOwner),
            BusinessOwnerDisplayName = string.IsNullOrWhiteSpace(ticket.BusinessOwner)
                ? null
                : context.ResolveOwnerFieldDisplayName(ticket.BusinessOwner),
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

    public static TicketAttachmentResponse ToResponse(
        this ArchivedTicketAttachment attachment,
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
            BoardId = rule.BoardId ?? string.Empty,
            Priority = rule.Priority ?? string.Empty,
            RequesterDepartment = rule.RequesterDepartment ?? string.Empty,
            RequesterRole = rule.RequesterRole ?? string.Empty,
            RulePriority = rule.RulePriority,
            Weight = rule.Weight,
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

public static class TicketRoutingDecisionMappings
{
    public static TicketRoutingDecisionResponse ToResponse(this TicketRoutingDecision decision)
    {
        return new TicketRoutingDecisionResponse
        {
            Id = decision.Id,
            TicketId = decision.TicketId ?? string.Empty,
            MatchedRuleId = decision.MatchedRuleId,
            OutcomeType = decision.OutcomeType.ToString(),
            ConfidenceLevel = decision.ConfidenceLevel.ToString(),
            NoMatchReason = decision.NoMatchReason?.ToString(),
            ChosenSynitiOwner = decision.ChosenSynitiOwner ?? string.Empty,
            ChosenBusinessOwner = decision.ChosenBusinessOwner ?? string.Empty,
            PrecedenceScore = decision.PrecedenceScore,
            TieBreakKey = decision.TieBreakKey ?? string.Empty,
            ExplanationJson = string.IsNullOrWhiteSpace(decision.ExplanationJson)
                ? "{}"
                : decision.ExplanationJson,
            ExplanationText = decision.ExplanationText ?? string.Empty,
            EngineVersion = decision.EngineVersion ?? string.Empty,
            CreatedDateUtc = decision.CreatedDateUtc
        };
    }

    public static TicketRoutingDecisionResponse ToPreviewResponse(
        this RoutingDecisionResult result,
        string ticketId)
    {
        return new TicketRoutingDecisionResponse
        {
            Id = 0,
            TicketId = ticketId,
            MatchedRuleId = result.MatchedRuleId,
            OutcomeType = result.OutcomeType.ToString(),
            ConfidenceLevel = result.ConfidenceLevel.ToString(),
            NoMatchReason = result.NoMatchReason?.ToString(),
            ChosenSynitiOwner = result.RecommendedSynitiOwner ?? string.Empty,
            ChosenBusinessOwner = result.RecommendedBusinessOwner ?? string.Empty,
            PrecedenceScore = result.PrecedenceScore,
            TieBreakKey = result.TieBreakKey,
            ExplanationJson = result.ExplanationJson,
            ExplanationText = result.ExplanationText,
            EngineVersion = result.EngineVersion,
            CreatedDateUtc = DateTime.UtcNow
        };
    }

    public static TicketRoutingOverrideResponse ToResponse(this TicketRoutingOverride @override)
    {
        return new TicketRoutingOverrideResponse
        {
            Id = @override.Id,
            TicketId = @override.TicketId ?? string.Empty,
            OverriddenByUserId = @override.OverriddenByUserId,
            PreviousSynitiOwner = @override.PreviousSynitiOwner ?? string.Empty,
            PreviousBusinessOwner = @override.PreviousBusinessOwner ?? string.Empty,
            NewSynitiOwner = @override.NewSynitiOwner ?? string.Empty,
            NewBusinessOwner = @override.NewBusinessOwner ?? string.Empty,
            OverrideReasonType = @override.OverrideReasonType.ToString(),
            OverrideReasonText = @override.OverrideReasonText ?? string.Empty,
            CreatedDateUtc = @override.CreatedDateUtc
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

public static class RoleDefinitionMappings
{
    public static RoleDefinitionResponse ToResponse(this RoleDefinition definition)
    {
        return new RoleDefinitionResponse
        {
            Id = definition.Id,
            Name = definition.Name,
            Description = definition.Description,
            Permissions = definition.Permissions,
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

public static class AiSettingsMappings
{
    public static AiSettingsResponse ToResponse(this AiSettingsConfiguration configuration)
    {
        return new AiSettingsResponse
        {
            IsIntakeAssistEnabled = configuration.IsIntakeAssistEnabled,
            IsTriageEnabled = configuration.IsTriageEnabled,
            IsScreenshotInsightEnabled = configuration.IsScreenshotInsightEnabled,
            IsSuggestedUpdatesEnabled = configuration.IsSuggestedUpdatesEnabled,
            IsPriorityRecommendationEnabled = configuration.IsPriorityRecommendationEnabled,
            IsStatusRecommendationEnabled = configuration.IsStatusRecommendationEnabled,
            DefaultTextModel = configuration.DefaultTextModel,
            DefaultVisionModel = configuration.DefaultVisionModel,
            Temperature = configuration.Temperature,
            MaxTokens = configuration.MaxTokens,
            TimeoutSeconds = configuration.TimeoutSeconds,
            RetryCount = configuration.RetryCount,
            AdvisoryOnlyMode = configuration.AdvisoryOnlyMode,
            AllowStatusRecommendation = configuration.AllowStatusRecommendation,
            AllowPriorityRecommendation = configuration.AllowPriorityRecommendation,
            SuggestionOnlyMode = configuration.SuggestionOnlyMode,
            ConfidenceThreshold = configuration.ConfidenceThreshold,
            MaxScreenshotAttachmentCount = configuration.MaxScreenshotAttachmentCount,
            LastModifiedByUserId = configuration.LastModifiedBy,
            LastModifiedByDisplayName = string.IsNullOrWhiteSpace(configuration.LastModifiedByUser?.DisplayName)
                ? configuration.LastModifiedByUser?.Email
                : configuration.LastModifiedByUser.DisplayName,
            LastModifiedDateUtc = configuration.LastModifiedDateUtc?.ToString("O"),
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
        ResponseMappingContext? mappingContext = null,
        bool includeSensitiveDetails = true)
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
            RunAsUserId = includeSensitiveDetails ? job.RunAsUserId : 0,
            RunAsDisplayName = includeSensitiveDetails
                ? context.ResolveUserDisplayName(
                    job.RunAsUserId,
                    job.RunAsUser)
                : "Restricted",
            CreatedDateUtc = job.CreatedDateUtc,
            LastModifiedDateUtc = job.LastModifiedDateUtc,
            LastRunDateUtc = job.LastRunDateUtc,
            NextRunDateUtc = job.NextRunDateUtc,
            LastRunStatus = job.LastRunStatus,
            LastRunMessage = includeSensitiveDetails ? job.LastRunMessage : null
        };
    }
}
