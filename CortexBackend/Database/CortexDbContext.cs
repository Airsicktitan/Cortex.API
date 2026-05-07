using Microsoft.EntityFrameworkCore;
using Cortex.API.Models;

namespace Cortex.API.Database;

public class CortexDbContext : DbContext
{
    public CortexDbContext(DbContextOptions<CortexDbContext> options)
        : base(options)
    {
    }

    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<ArchivedTicket> ArchivedTickets => Set<ArchivedTicket>();
    public DbSet<ArchivedComment> ArchivedComments => Set<ArchivedComment>();
    public DbSet<ArchivedTicketAttachment> ArchivedTicketAttachments => Set<ArchivedTicketAttachment>();
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<TicketAuditEntry> TicketAuditEntries => Set<TicketAuditEntry>();
    public DbSet<TicketAuditFieldChange> TicketAuditFieldChanges => Set<TicketAuditFieldChange>();
    public DbSet<SlaConfiguration> SlaConfigurations => Set<SlaConfiguration>();
    public DbSet<ArchiveConfiguration> ArchiveConfigurations => Set<ArchiveConfiguration>();
    public DbSet<SessionConfiguration> SessionConfigurations => Set<SessionConfiguration>();
    public DbSet<NotificationChannelConfiguration> NotificationChannelConfigurations => Set<NotificationChannelConfiguration>();
    public DbSet<AiSettingsConfiguration> AiSettingsConfigurations => Set<AiSettingsConfiguration>();
    public DbSet<AiSettingsAuditEntry> AiSettingsAuditEntries => Set<AiSettingsAuditEntry>();
    public DbSet<ReportDefinition> ReportDefinitions => Set<ReportDefinition>();
    public DbSet<StoredProcedureDefinition> StoredProcedureDefinitions => Set<StoredProcedureDefinition>();
    public DbSet<TicketStatusDefinition> TicketStatusDefinitions => Set<TicketStatusDefinition>();
    public DbSet<RoleDefinition> RoleDefinitions => Set<RoleDefinition>();
    public DbSet<TicketRoutingRule> TicketRoutingRules => Set<TicketRoutingRule>();
    public DbSet<TicketRoutingDecision> TicketRoutingDecisions => Set<TicketRoutingDecision>();
    public DbSet<TicketRoutingOverride> TicketRoutingOverrides => Set<TicketRoutingOverride>();
    public DbSet<TicketBoardDefinition> TicketBoardDefinitions => Set<TicketBoardDefinition>();
    public DbSet<ScheduledJob> ScheduledJobs => Set<ScheduledJob>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<HttpRequestLogEntry> HttpRequestLogEntries => Set<HttpRequestLogEntry>();
    public DbSet<WorkflowMetricEvent> WorkflowMetricEvents => Set<WorkflowMetricEvent>();
    public DbSet<TicketEmbedding> TicketEmbeddings => Set<TicketEmbedding>();
    public DbSet<CortexMemoryFeedbackEvent> CortexMemoryFeedbackEvents => Set<CortexMemoryFeedbackEvent>();
    public DbSet<TicketOutcome> TicketOutcomes => Set<TicketOutcome>();
    public DbSet<CortexSystemRecommendationState> CortexSystemRecommendationStates => Set<CortexSystemRecommendationState>();
    public DbSet<CortexAutonomyDecision> CortexAutonomyDecisions => Set<CortexAutonomyDecision>();
    public DbSet<CortexAutonomyConfiguration> CortexAutonomyConfigurations => Set<CortexAutonomyConfiguration>();
    public DbSet<IntegrationConnection> IntegrationConnections => Set<IntegrationConnection>();
    public DbSet<ExternalWorkSource> ExternalWorkSources => Set<ExternalWorkSource>();
    public DbSet<ExternalBoardMapping> ExternalBoardMappings => Set<ExternalBoardMapping>();
    public DbSet<ExternalFieldMapping> ExternalFieldMappings => Set<ExternalFieldMapping>();
    public DbSet<ExternalWorkItem> ExternalWorkItems => Set<ExternalWorkItem>();
    public DbSet<IntegrationActivityLog> IntegrationActivityLogs => Set<IntegrationActivityLog>();
    public DbSet<SapReferenceSource> SapReferenceSources => Set<SapReferenceSource>();
    public DbSet<SapTableMetadata> SapTables => Set<SapTableMetadata>();
    public DbSet<SapFieldMetadata> SapFields => Set<SapFieldMetadata>();
    public DbSet<SapDomainValueMetadata> SapDomainValues => Set<SapDomainValueMetadata>();
    public DbSet<SynitiKnowledgeSource> SynitiKnowledgeSources => Set<SynitiKnowledgeSource>();
    public DbSet<SynitiKnowledgeEntry> SynitiKnowledgeEntries => Set<SynitiKnowledgeEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Ticket>(entity =>
        {
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(t => t.Status)
                .IsRequired();

            entity.Property(t => t.Priority)
                .IsRequired();

            entity.Property(t => t.ApprovalStatus)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();

            entity.Property(t => t.RejectionReason).HasMaxLength(2000);
            entity.Property(t => t.ReturnReason).HasMaxLength(2000);

            entity.Property(t => t.BoardId)
                .IsRequired();

            entity.HasIndex(t => t.ApprovalStatus);
            entity.HasIndex(t => t.Status);
            entity.HasIndex(t => t.Priority);
            entity.HasIndex(t => t.BoardId);
            entity.HasOne(t => t.CreatedByUser)
                .WithMany()
                .HasForeignKey(t => t.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.BoardDefinition)
                .WithMany()
                .HasForeignKey(t => t.BoardId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.Property(t => t.RowVersion)
                .IsRowVersion();
        });

        modelBuilder.Entity<ArchivedTicket>(entity =>
        {
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(t => t.Status)
                .IsRequired();

            entity.Property(t => t.Priority)
                .IsRequired();

            entity.Property(t => t.BoardId)
                .IsRequired();

            entity.Property(t => t.ArchivedDate)
                .IsRequired();

            entity.Property(t => t.CommentCount)
                .IsRequired();

            entity.Property(t => t.AttachmentCount)
                .IsRequired();

            entity.HasIndex(t => t.Status);
            entity.HasIndex(t => t.Priority);
            entity.HasIndex(t => t.BoardId);
            entity.HasIndex(t => t.ArchivedDate);

            entity.HasOne(t => t.CreatedByUser)
                .WithMany()
                .HasForeignKey(t => t.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.ArchivedByUser)
                .WithMany()
                .HasForeignKey(t => t.ArchivedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.BoardDefinition)
                .WithMany()
                .HasForeignKey(t => t.BoardId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ArchivedComment>(entity =>
        {
            entity.HasKey(comment => comment.Id);

            entity.Property(comment => comment.Body)
                .IsRequired();

            entity.HasIndex(comment => comment.TicketId);

            entity.HasOne(comment => comment.ArchivedTicket)
                .WithMany()
                .HasForeignKey(comment => comment.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(comment => comment.CreatedByUser)
                .WithMany()
                .HasForeignKey(comment => comment.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ArchivedTicketAttachment>(entity =>
        {
            entity.HasKey(attachment => attachment.Id);

            entity.Property(attachment => attachment.FileName)
                .IsRequired()
                .HasMaxLength(260);

            entity.Property(attachment => attachment.ContentType)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(attachment => attachment.Content)
                .IsRequired();

            entity.Property(attachment => attachment.FileSize)
                .IsRequired();

            entity.HasIndex(attachment => attachment.TicketId);

            entity.HasOne(attachment => attachment.ArchivedTicket)
                .WithMany()
                .HasForeignKey(attachment => attachment.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(attachment => attachment.UploadedByUser)
                .WithMany()
                .HasForeignKey(attachment => attachment.UploadedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TicketAttachment>(entity =>
        {
            entity.HasKey(a => a.Id);

            entity.Property(a => a.FileName)
                .IsRequired()
                .HasMaxLength(260);

            entity.Property(a => a.ContentType)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(a => a.Content)
                .IsRequired();

            entity.Property(a => a.FileSize)
                .IsRequired();

            entity.HasIndex(a => a.TicketId);

            entity.HasOne(a => a.Ticket)
                .WithMany(t => t.Attachments)
                .HasForeignKey(a => a.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(a => a.UploadedByUser)
                .WithMany()
                .HasForeignKey(a => a.UploadedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);

            entity.Property(u => u.DisplayName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(u => u.NickName)
                .HasMaxLength(100);

            entity.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(u => u.PhoneNumber)
                .HasMaxLength(50);

            entity.Property(u => u.AssignmentNotificationChannel)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.Property(u => u.SlaRiskNotificationChannel)
                .HasConversion<string>()
                .HasMaxLength(20);

            entity.HasIndex(u => u.Email)
                .IsUnique();

            entity.Property(u => u.Role)
                .IsRequired()
                .HasMaxLength(50);
            
            entity.HasIndex(u => u.Auth0Id)
                .IsUnique()
                .HasFilter("[Auth0Id] IS NOT NULL")
                .IsUnique();
        });

        modelBuilder.Entity<SlaConfiguration>(entity =>
        {
            entity.HasKey(s => s.Priority);

            entity.Property(s => s.Priority)
                .HasMaxLength(50);

            entity.Property(s => s.TargetHours)
                .IsRequired();

            entity.Property(s => s.WarningHours)
                .IsRequired();
        });

        modelBuilder.Entity<ArchiveConfiguration>(entity =>
        {
            entity.HasKey(configuration => configuration.Id);

            entity.Property(configuration => configuration.ArchiveAfterDays)
                .IsRequired();

            entity.Property(configuration => configuration.EligibleStatusesJson)
                .IsRequired();
        });

        modelBuilder.Entity<TicketStatusDefinition>(entity =>
        {
            entity.HasKey(definition => definition.Id);

            entity.Property(definition => definition.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(definition => definition.Description)
                .HasMaxLength(500);

            entity.Property(definition => definition.IsEnabled)
                .IsRequired();

            entity.HasIndex(definition => definition.Name)
                .IsUnique();
        });

        modelBuilder.Entity<RoleDefinition>(entity =>
        {
            entity.HasKey(definition => definition.Id);

            entity.Property(definition => definition.Name)
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(definition => definition.NameNormalized)
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(definition => definition.Description)
                .HasMaxLength(500);

            entity.Property(definition => definition.PermissionsJson)
                .IsRequired();

            entity.Property(definition => definition.IsEnabled)
                .IsRequired();

            entity.HasIndex(definition => definition.Name);

            entity.HasIndex(definition => definition.NameNormalized)
                .IsUnique();
        });

        modelBuilder.Entity<TicketRoutingRule>(entity =>
        {
            entity.HasKey(rule => rule.Id);

            entity.Property(rule => rule.BoardId)
                .HasMaxLength(100);

            entity.Property(rule => rule.Priority)
                .HasMaxLength(50);

            entity.Property(rule => rule.RequesterDepartment)
                .HasMaxLength(120);

            entity.Property(rule => rule.RequesterRole)
                .HasMaxLength(80);

            entity.Property(rule => rule.RulePriority)
                .IsRequired();

            entity.Property(rule => rule.Weight)
                .IsRequired();

            entity.Property(rule => rule.Department)
                .HasMaxLength(120);

            entity.Property(rule => rule.TitleContains)
                .HasMaxLength(200);

            entity.Property(rule => rule.SynitiOwner)
                .HasMaxLength(200);

            entity.Property(rule => rule.BusinessOwner)
                .HasMaxLength(200);

            entity.Property(rule => rule.IsEnabled)
                .IsRequired();

            entity.HasIndex(rule => rule.Department);
            entity.HasIndex(rule => rule.TitleContains);
            entity.HasIndex(rule => rule.BoardId);
            entity.HasIndex(rule => rule.Priority);
            entity.HasIndex(rule => rule.RequesterDepartment);
            entity.HasIndex(rule => rule.RequesterRole);
            entity.HasIndex(rule => new { rule.Department, rule.TitleContains });
        });

        modelBuilder.Entity<TicketRoutingDecision>(entity =>
        {
            entity.HasKey(decision => decision.Id);
            entity.Property(decision => decision.TicketId)
                .IsRequired()
                .HasMaxLength(450);
            entity.Property(decision => decision.ExplanationJson)
                .IsRequired();
            entity.Property(decision => decision.ExplanationText)
                .IsRequired()
                .HasMaxLength(2000);
            entity.Property(decision => decision.TieBreakKey)
                .IsRequired()
                .HasMaxLength(300);
            entity.Property(decision => decision.EngineVersion)
                .IsRequired()
                .HasMaxLength(80);
            entity.Property(decision => decision.OutcomeType)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(decision => decision.ConfidenceLevel)
                .HasConversion<string>()
                .HasMaxLength(20);
            entity.Property(decision => decision.NoMatchReason)
                .HasConversion<string>()
                .HasMaxLength(30);
            entity.HasIndex(decision => new { decision.TicketId, decision.CreatedDateUtc });
            entity.HasOne<Ticket>()
                .WithMany()
                .HasForeignKey(decision => decision.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(decision => decision.MatchedRule)
                .WithMany()
                .HasForeignKey(decision => decision.MatchedRuleId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<TicketRoutingOverride>(entity =>
        {
            entity.HasKey(@override => @override.Id);
            entity.Property(@override => @override.TicketId)
                .IsRequired()
                .HasMaxLength(450);
            entity.Property(@override => @override.OverrideReasonType)
                .HasConversion<string>()
                .HasMaxLength(40);
            entity.Property(@override => @override.OverrideReasonText)
                .HasMaxLength(1000);
            entity.Property(@override => @override.DecisionImpactAssignmentField)
                .HasMaxLength(40);
            entity.Property(@override => @override.DecisionImpactPreviousPressureLevel)
                .HasMaxLength(20);
            entity.Property(@override => @override.DecisionImpactPreviousRiskLevel)
                .HasMaxLength(20);
            entity.Property(@override => @override.DecisionImpactPreviousSlaStatus)
                .HasMaxLength(40);
            entity.Property(@override => @override.DecisionImpactSource)
                .HasMaxLength(80);
            entity.HasIndex(@override => new { @override.TicketId, @override.CreatedDateUtc });
            entity.HasOne<Ticket>()
                .WithMany()
                .HasForeignKey(@override => @override.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(@override => @override.OverriddenByUser)
                .WithMany()
                .HasForeignKey(@override => @override.OverriddenByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<TicketBoardDefinition>(entity =>
        {
            var seededDateUtc = new DateTime(2026, 4, 12, 0, 0, 0, DateTimeKind.Utc);

            entity.HasKey(definition => definition.Id);

            entity.Property(definition => definition.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(definition => definition.Description)
                .HasMaxLength(500);

            entity.Property(definition => definition.RequiresStoryPoints)
                .IsRequired();

            entity.Property(definition => definition.IsEnabled)
                .IsRequired();

            entity.HasIndex(definition => definition.Name)
                .IsUnique();

            entity.HasData(
                new TicketBoardDefinition
                {
                    Id = 1,
                    Name = "Ticket",
                    Description = "Standard operational ticket board.",
                    RequiresStoryPoints = false,
                    IsEnabled = true,
                    CreatedDateUtc = seededDateUtc
                },
                new TicketBoardDefinition
                {
                    Id = 2,
                    Name = "Hypercare",
                    Description = "High-touch stabilization and production support work.",
                    RequiresStoryPoints = false,
                    IsEnabled = true,
                    CreatedDateUtc = seededDateUtc
                },
                new TicketBoardDefinition
                {
                    Id = 3,
                    Name = "Enhancement",
                    Description = "Planned improvements and backlog work.",
                    RequiresStoryPoints = true,
                    IsEnabled = true,
                    CreatedDateUtc = seededDateUtc
                });
        });

        modelBuilder.Entity<SessionConfiguration>(entity =>
        {
            entity.HasKey(configuration => configuration.Id);

            entity.Property(configuration => configuration.InactivityTimeoutMinutes)
                .IsRequired();

            entity.Property(configuration => configuration.WarningMinutes)
                .IsRequired();
        });

        modelBuilder.Entity<NotificationChannelConfiguration>(entity =>
        {
            entity.HasKey(configuration => configuration.Id);

            entity.Property(configuration => configuration.AssignmentChannel)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(configuration => configuration.SlaRiskChannel)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();
        });

        modelBuilder.Entity<AiSettingsConfiguration>(entity =>
        {
            entity.HasKey(configuration => configuration.Id);

            entity.Property(configuration => configuration.DefaultTextModel)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(configuration => configuration.DefaultVisionModel)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(configuration => configuration.Temperature)
                .IsRequired();

            entity.Property(configuration => configuration.MaxTokens)
                .IsRequired();

            entity.Property(configuration => configuration.TimeoutSeconds)
                .IsRequired();

            entity.Property(configuration => configuration.RetryCount)
                .IsRequired();

            entity.Property(configuration => configuration.ConfidenceThreshold)
                .IsRequired();

            entity.Property(configuration => configuration.MaxScreenshotAttachmentCount)
                .IsRequired();

            entity.HasOne(configuration => configuration.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(configuration => configuration.LastModifiedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<AiSettingsAuditEntry>(entity =>
        {
            entity.HasKey(entry => entry.Id);

            entity.Property(entry => entry.ChangedDateUtc)
                .IsRequired();

            entity.Property(entry => entry.BeforeSnapshotJson)
                .IsRequired();

            entity.Property(entry => entry.AfterSnapshotJson)
                .IsRequired();

            entity.HasIndex(entry => entry.ChangedDateUtc);

            entity.HasOne(entry => entry.ChangedByUser)
                .WithMany()
                .HasForeignKey(entry => entry.ChangedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ReportDefinition>(entity =>
        {
            entity.HasKey(definition => definition.Id);

            entity.Property(definition => definition.Name)
                .IsRequired()
                .HasMaxLength(120);

            entity.Property(definition => definition.ViewName)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(definition => definition.Description)
                .HasMaxLength(500);

            entity.Property(definition => definition.SqlQuery)
                .IsRequired();

            entity.Property(definition => definition.IsEnabled)
                .IsRequired();

            entity.HasIndex(definition => definition.Name)
                .IsUnique();

            entity.HasIndex(definition => definition.ViewName)
                .IsUnique();
        });

        modelBuilder.Entity<StoredProcedureDefinition>(entity =>
        {
            entity.HasKey(definition => definition.Id);

            entity.Property(definition => definition.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(definition => definition.ProcedureName)
                .IsRequired()
                .HasMaxLength(256);

            entity.Property(definition => definition.DefinitionSql)
                .IsRequired();

            entity.Property(definition => definition.Description)
                .HasMaxLength(500);

            entity.Property(definition => definition.IsEnabled)
                .IsRequired();

            entity.HasIndex(definition => definition.Name)
                .IsUnique();

            entity.HasIndex(definition => definition.ProcedureName)
                .IsUnique();
        });

        modelBuilder.Entity<ScheduledJob>(entity =>
        {
            entity.HasKey(job => job.Id);

            entity.Property(job => job.Name)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(job => job.Description)
                .HasMaxLength(500);

            entity.Property(job => job.JobType)
                .HasConversion<string>()
                .HasMaxLength(50);

            entity.Property(job => job.IntervalMinutes)
                .IsRequired();

            entity.Property(job => job.IsEnabled)
                .IsRequired();

            entity.Property(job => job.LastRunStatus)
                .HasMaxLength(50);

            entity.Property(job => job.LastRunMessage)
                .HasMaxLength(1000);

            entity.HasIndex(job => job.Name)
                .IsUnique();

            entity.HasIndex(job => job.NextRunDateUtc);

            entity.HasOne(job => job.StoredProcedureDefinition)
                .WithMany()
                .HasForeignKey(job => job.StoredProcedureDefinitionId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(job => job.RunAsUser)
                .WithMany()
                .HasForeignKey(job => job.RunAsUserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<UserNotification>(entity =>
        {
            entity.HasKey(notification => notification.Id);

            entity.Property(notification => notification.Category)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(notification => notification.EventType)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(notification => notification.Severity)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(notification => notification.Title)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(notification => notification.Message)
                .IsRequired()
                .HasMaxLength(1000);

            entity.Property(notification => notification.TicketId)
                .HasMaxLength(50);

            entity.Property(notification => notification.DeduplicationKey)
                .HasMaxLength(200);

            entity.HasIndex(notification => new
                { notification.UserId, notification.CreatedDateUtc });

            entity.HasIndex(notification => new
                { notification.UserId, notification.IsRead, notification.CreatedDateUtc });

            entity.HasIndex(notification => new
                { notification.UserId, notification.DeduplicationKey })
                .IsUnique()
                .HasFilter("[DeduplicationKey] IS NOT NULL");

            entity.HasOne(notification => notification.User)
                .WithMany()
                .HasForeignKey(notification => notification.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Comment>()
            .HasOne(c => c.CreatedByUser)
            .WithMany()
            .HasForeignKey(c => c.CreatedBy)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<TicketAuditEntry>(entity =>
        {
            entity.HasKey(entry => entry.Id);

            entity.Property(entry => entry.Action)
                .IsRequired()
                .HasMaxLength(50);

            entity.Property(entry => entry.Summary)
                .IsRequired()
                .HasMaxLength(250);

            entity.Property(entry => entry.Reason)
                .HasMaxLength(1000);

            entity.Property(entry => entry.ChangedDateUtc)
                .IsRequired();

            entity.HasIndex(entry => new { entry.TicketId, entry.ChangedDateUtc });

            entity.HasOne(entry => entry.ChangedByUser)
                .WithMany()
                .HasForeignKey(entry => entry.ChangedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasMany(entry => entry.FieldChanges)
                .WithOne(change => change.TicketAuditEntry)
                .HasForeignKey(change => change.TicketAuditEntryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TicketAuditFieldChange>(entity =>
        {
            entity.HasKey(change => change.Id);

            entity.Property(change => change.FieldName)
                .IsRequired()
                .HasMaxLength(100);
        });

        modelBuilder.Entity<HttpRequestLogEntry>(entity =>
        {
            entity.HasKey(entry => entry.Id);

            entity.Property(entry => entry.OccurredUtc)
                .IsRequired();

            entity.Property(entry => entry.Method)
                .IsRequired()
                .HasMaxLength(16);

            entity.Property(entry => entry.Path)
                .IsRequired()
                .HasMaxLength(2048);

            entity.Property(entry => entry.TraceId)
                .IsRequired()
                .HasMaxLength(128);

            entity.HasIndex(entry => entry.OccurredUtc);
        });

        modelBuilder.Entity<WorkflowMetricEvent>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.EventType)
                .IsRequired()
                .HasMaxLength(128);

            entity.Property(e => e.OccurredUtc)
                .IsRequired();

            entity.Property(e => e.TicketId)
                .HasMaxLength(64);

            entity.Property(e => e.PayloadJson)
                .IsRequired();

            entity.HasIndex(e => e.OccurredUtc);
            entity.HasIndex(e => e.EventType);
            entity.HasIndex(e => e.TicketId);
        });

        modelBuilder.Entity<TicketEmbedding>(entity =>
        {
            entity.HasKey(embedding => new { embedding.TicketId, embedding.EmbeddingModel });

            entity.Property(embedding => embedding.TicketId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(embedding => embedding.EmbeddingModel)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(embedding => embedding.ContentHash)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(embedding => embedding.VectorJson)
                .IsRequired();

            entity.Property(embedding => embedding.CreatedAtUtc)
                .IsRequired();

            entity.Property(embedding => embedding.UpdatedAtUtc)
                .IsRequired();

            entity.HasIndex(embedding => embedding.ContentHash);
            entity.HasIndex(embedding => embedding.UpdatedAtUtc);

            entity.HasOne<Ticket>()
                .WithMany()
                .HasForeignKey(embedding => embedding.TicketId)
                .OnDelete(DeleteBehavior.Cascade);

            // Future Cortex Memory v2 vector similarity search plugs in here:
            // replace/augment keyword candidate retrieval with nearest-neighbor lookup over VectorJson.
        });

        modelBuilder.Entity<CortexMemoryFeedbackEvent>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.TicketId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(e => e.RelatedTicketId)
                .HasMaxLength(450);

            entity.Property(e => e.EventType)
                .IsRequired()
                .HasMaxLength(64);

            entity.Property(e => e.Source)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(e => e.CreatedByDisplayName)
                .HasMaxLength(200);

            entity.HasIndex(e => new { e.TicketId, e.EventType });
            entity.HasIndex(e => e.CreatedAtUtc);
        });

        modelBuilder.Entity<TicketOutcome>(entity =>
        {
            entity.HasKey(o => o.Id);

            entity.Property(o => o.TicketId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(o => o.AssignedSynitiOwner).HasMaxLength(200);
            entity.Property(o => o.AssignedBusinessOwner).HasMaxLength(200);
            entity.Property(o => o.FinalSynitiOwner).HasMaxLength(200);
            entity.Property(o => o.FinalBusinessOwner).HasMaxLength(200);

            entity.HasIndex(o => o.TicketId).IsUnique();
            entity.HasIndex(o => o.BoardId);
            entity.HasIndex(o => o.MatchedRuleId);
            entity.HasIndex(o => o.ReachedTerminalStatus);
            entity.HasIndex(o => o.FinalSynitiOwner);
            entity.HasIndex(o => o.FinalBusinessOwner);
        });

        modelBuilder.Entity<CortexSystemRecommendationState>(entity =>
        {
            entity.HasKey(s => s.Id);

            entity.Property(s => s.RecommendationId)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(s => s.Status)
                .IsRequired()
                .HasMaxLength(20);
            entity.Property(s => s.DismissedReason)
                .HasMaxLength(1000);
            entity.Property(s => s.CreatedAtUtc)
                .IsRequired();

            entity.HasIndex(s => s.RecommendationId)
                .IsUnique();
            entity.HasIndex(s => s.Status);
            entity.HasIndex(s => s.ReviewedAtUtc);
        });

        modelBuilder.Entity<CortexAutonomyConfiguration>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Enabled).IsRequired();
            entity.Property(c => c.ShadowMode).IsRequired();
            entity.Property(c => c.MinConfidence).IsRequired();
            entity.Property(c => c.RecentOverrideWindowHours).IsRequired();
            entity.Property(c => c.RequireClearWinner).IsRequired();
            entity.Property(c => c.MinAlternativeGap).IsRequired();
            entity.HasOne(c => c.LastModifiedByUser)
                .WithMany()
                .HasForeignKey(c => c.LastModifiedBy)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<CortexAutonomyDecision>(entity =>
        {
            entity.HasKey(d => d.Id);

            entity.Property(d => d.TicketId)
                .IsRequired()
                .HasMaxLength(450);

            entity.Property(d => d.RecommendedOwnerId).HasMaxLength(200);
            entity.Property(d => d.RecommendedOwnerName).HasMaxLength(200);
            entity.Property(d => d.PreviousOwnerId).HasMaxLength(200);

            entity.Property(d => d.Confidence)
                .HasPrecision(5, 4);
            entity.Property(d => d.LearningAdjustment)
                .HasPrecision(5, 4);

            entity.Property(d => d.Mode)
                .IsRequired()
                .HasMaxLength(20);

            entity.Property(d => d.PassedChecksJson)
                .IsRequired();
            entity.Property(d => d.BlockedReasonsJson)
                .IsRequired();

            entity.Property(d => d.Summary)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(d => d.DecisionVersion)
                .IsRequired()
                .HasMaxLength(40);

            entity.Property(d => d.CreatedDateUtc).IsRequired();

            entity.HasIndex(d => new { d.TicketId, d.CreatedDateUtc });
            entity.HasIndex(d => d.WasAutoApplied);

            entity.HasOne<Ticket>()
                .WithMany()
                .HasForeignKey(d => d.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IntegrationConnection>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Provider)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(c => c.DisplayName)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(c => c.TenantId).HasMaxLength(200);
            entity.Property(c => c.OrganizationId).HasMaxLength(200);
            entity.Property(c => c.AuthMode)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(c => c.SyncMode)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(c => c.LastSyncStatus).HasMaxLength(50);
            entity.Property(c => c.LastSyncMessage).HasMaxLength(2000);
            entity.Property(c => c.CreatedAtUtc).IsRequired();
            entity.HasIndex(c => c.Provider);
            entity.HasMany(c => c.ExternalWorkSources)
                .WithOne(s => s.IntegrationConnection)
                .HasForeignKey(s => s.IntegrationConnectionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExternalWorkSource>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Provider)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(s => s.SourceType)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(s => s.ExternalSourceId)
                .IsRequired()
                .HasMaxLength(450);
            entity.Property(s => s.Name)
                .IsRequired()
                .HasMaxLength(500);
            entity.Property(s => s.ExternalUrl).HasMaxLength(2000);
            entity.Property(s => s.CreatedAtUtc).IsRequired();
            entity.HasIndex(s => s.IntegrationConnectionId);
            entity.HasIndex(s => new { s.IntegrationConnectionId, s.ExternalSourceId })
                .IsUnique();
            entity.HasMany(s => s.BoardMappings)
                .WithOne(m => m.ExternalWorkSource)
                .HasForeignKey(m => m.ExternalWorkSourceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(s => s.FieldMappings)
                .WithOne(m => m.ExternalWorkSource)
                .HasForeignKey(m => m.ExternalWorkSourceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(s => s.WorkItems)
                .WithOne(i => i.ExternalWorkSource)
                .HasForeignKey(i => i.ExternalWorkSourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ExternalBoardMapping>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.MappingMode)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(m => m.CreatedAtUtc).IsRequired();
            entity.HasOne(m => m.Board)
                .WithMany()
                .HasForeignKey(m => m.BoardId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ExternalFieldMapping>(entity =>
        {
            entity.HasKey(m => m.Id);
            entity.Property(m => m.ExternalFieldName)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(m => m.ExternalFieldKey).HasMaxLength(200);
            entity.Property(m => m.CortexField)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(m => m.TransformHint).HasMaxLength(500);
            entity.Property(m => m.CreatedAtUtc).IsRequired();
        });

        modelBuilder.Entity<ExternalWorkItem>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.Property(i => i.Provider)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(i => i.ExternalItemId)
                .IsRequired()
                .HasMaxLength(450);
            entity.Property(i => i.ExternalUrl).HasMaxLength(2000);
            entity.Property(i => i.Title)
                .IsRequired()
                .HasMaxLength(500);
            entity.Property(i => i.Status).HasMaxLength(200);
            entity.Property(i => i.Priority).HasMaxLength(100);
            entity.Property(i => i.Requester).HasMaxLength(500);
            entity.Property(i => i.AssignedTo).HasMaxLength(500);
            entity.Property(i => i.Department).HasMaxLength(200);
            entity.Property(i => i.Category).HasMaxLength(200);
            entity.Property(i => i.RawJson)
                .IsRequired();
            entity.Property(i => i.SyncHash).HasMaxLength(64);
            entity.Property(i => i.CortexTicketId).HasMaxLength(450);
            entity.Property(i => i.LastSeenUtc).IsRequired();
            entity.Property(i => i.CreatedAtUtc).IsRequired();
            entity.HasIndex(i => new { i.ExternalWorkSourceId, i.ExternalItemId })
                .IsUnique();
            entity.HasIndex(i => i.CortexTicketId);
            entity.HasIndex(i => i.LastSeenUtc);
            entity.HasIndex(i => i.IsDeleted);
            entity.HasOne(i => i.CortexTicket)
                .WithMany()
                .HasForeignKey(i => i.CortexTicketId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<IntegrationActivityLog>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.ActivityType)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(a => a.Status)
                .HasConversion<string>()
                .HasMaxLength(16)
                .IsRequired();
            entity.Property(a => a.TriggeredByDisplayName).HasMaxLength(200);
            entity.Property(a => a.TriggeredByEmail).HasMaxLength(200);
            entity.Property(a => a.StartedAtUtc).IsRequired();
            entity.Property(a => a.CompletedAtUtc).IsRequired();
            entity.Property(a => a.Message).HasMaxLength(2000);
            entity.Property(a => a.ErrorMessage).HasMaxLength(2000);
            entity.Property(a => a.MetadataJson).HasMaxLength(2000);
            entity.HasIndex(a => new { a.ExternalWorkSourceId, a.StartedAtUtc });
            entity.HasOne(a => a.ExternalWorkSource)
                .WithMany()
                .HasForeignKey(a => a.ExternalWorkSourceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(a => a.IntegrationConnection)
                .WithMany()
                .HasForeignKey(a => a.IntegrationConnectionId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<SapReferenceSource>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Name)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(s => s.Description).HasMaxLength(2000);
            entity.Property(s => s.SourceType)
                .HasConversion<string>()
                .HasMaxLength(32)
                .IsRequired();
            entity.Property(s => s.SystemLabel).HasMaxLength(120);
            entity.Property(s => s.Client).HasMaxLength(12);
            entity.Property(s => s.Environment).HasMaxLength(80);
            entity.Property(s => s.CreatedAtUtc).IsRequired();
            entity.HasIndex(s => s.Name);
            entity.HasMany(s => s.Tables)
                .WithOne(t => t.SapReferenceSource)
                .HasForeignKey(t => t.SapReferenceSourceId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(s => s.DomainValues)
                .WithOne(d => d.SapReferenceSource)
                .HasForeignKey(d => d.SapReferenceSourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SapTableMetadata>(entity =>
        {
            entity.HasKey(t => t.Id);
            entity.Property(t => t.TableName)
                .IsRequired()
                .HasMaxLength(30);
            entity.Property(t => t.Description).HasMaxLength(2000);
            entity.Property(t => t.Module).HasMaxLength(20);
            entity.Property(t => t.BusinessObject).HasMaxLength(120);
            entity.Property(t => t.DataDomain).HasMaxLength(120);
            entity.Property(t => t.Notes).HasMaxLength(4000);
            entity.Property(t => t.CreatedAtUtc).IsRequired();
            entity.HasIndex(t => t.SapReferenceSourceId);
            entity.HasIndex(t => t.TableName);
            entity.HasIndex(t => t.Module);
            entity.HasIndex(t => t.BusinessObject);
            entity.HasIndex(t => t.IsCustom);
            entity.HasIndex(t => new { t.SapReferenceSourceId, t.TableName })
                .IsUnique();
            entity.HasMany(t => t.Fields)
                .WithOne(f => f.SapTableMetadata)
                .HasForeignKey(f => f.SapTableMetadataId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SapFieldMetadata>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.Property(f => f.FieldName)
                .IsRequired()
                .HasMaxLength(30);
            entity.Property(f => f.Description).HasMaxLength(2000);
            entity.Property(f => f.DataElement).HasMaxLength(30);
            entity.Property(f => f.DomainName).HasMaxLength(30);
            entity.Property(f => f.DataType).HasMaxLength(40);
            entity.Property(f => f.BusinessMeaning).HasMaxLength(2000);
            entity.Property(f => f.ExampleValue).HasMaxLength(500);
            entity.Property(f => f.Notes).HasMaxLength(4000);
            entity.Property(f => f.CreatedAtUtc).IsRequired();
            entity.HasIndex(f => f.SapTableMetadataId);
            entity.HasIndex(f => f.FieldName);
            entity.HasIndex(f => f.DataElement);
            entity.HasIndex(f => f.DomainName);
            entity.HasIndex(f => f.IsCustom);
            entity.HasIndex(f => new { f.SapTableMetadataId, f.FieldName })
                .IsUnique();
        });

        modelBuilder.Entity<SapDomainValueMetadata>(entity =>
        {
            entity.HasKey(d => d.Id);
            entity.Property(d => d.DomainName)
                .IsRequired()
                .HasMaxLength(30);
            entity.Property(d => d.Value)
                .IsRequired()
                .HasMaxLength(60);
            entity.Property(d => d.Description).HasMaxLength(2000);
            entity.Property(d => d.Notes).HasMaxLength(2000);
            entity.Property(d => d.CreatedAtUtc).IsRequired();
            entity.HasIndex(d => d.SapReferenceSourceId);
            entity.HasIndex(d => new { d.SapReferenceSourceId, d.DomainName, d.Value })
                .IsUnique();
        });

        modelBuilder.Entity<SynitiKnowledgeSource>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Name)
                .IsRequired()
                .HasMaxLength(200);
            entity.Property(s => s.SourceType)
                .HasConversion<string>()
                .HasMaxLength(40)
                .IsRequired();
            entity.Property(s => s.Version).HasMaxLength(80);
            entity.Property(s => s.CreatedAtUtc).IsRequired();
            entity.HasIndex(s => s.Name);
            entity.HasMany(s => s.Entries)
                .WithOne(e => e.SynitiKnowledgeSource)
                .HasForeignKey(e => e.SynitiKnowledgeSourceId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SynitiKnowledgeEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Term)
                .IsRequired()
                .HasMaxLength(240);
            entity.Property(e => e.Category)
                .HasConversion<string>()
                .HasMaxLength(40)
                .IsRequired();
            entity.Property(e => e.ShortDefinition)
                .IsRequired()
                .HasMaxLength(8000);
            entity.Property(e => e.BusinessMeaning).HasMaxLength(8000);
            entity.Property(e => e.TechnicalMeaning).HasMaxLength(8000);
            entity.Property(e => e.CommonSignals).HasMaxLength(8000);
            entity.Property(e => e.RelatedTerms).HasMaxLength(8000);
            entity.Property(e => e.ExamplePhrases).HasMaxLength(8000);
            entity.Property(e => e.Aliases).HasMaxLength(2000);
            entity.Property(e => e.SuggestedReviewerChecks).HasMaxLength(8000);
            entity.Property(e => e.MissingContextQuestions).HasMaxLength(8000);
            entity.Property(e => e.CreatedAtUtc).IsRequired();
            entity.HasIndex(e => e.SynitiKnowledgeSourceId);
            entity.HasIndex(e => new { e.SynitiKnowledgeSourceId, e.Term })
                .IsUnique();
        });
    }
}
