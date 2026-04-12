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
    public DbSet<ReportDefinition> ReportDefinitions => Set<ReportDefinition>();
    public DbSet<StoredProcedureDefinition> StoredProcedureDefinitions => Set<StoredProcedureDefinition>();
    public DbSet<TicketStatusDefinition> TicketStatusDefinitions => Set<TicketStatusDefinition>();
    public DbSet<TicketRoutingRule> TicketRoutingRules => Set<TicketRoutingRule>();
    public DbSet<ScheduledJob> ScheduledJobs => Set<ScheduledJob>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();

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

            entity.HasIndex(t => t.Status);
            entity.HasIndex(t => t.Priority);
            entity.HasOne(t => t.CreatedByUser)
                .WithMany()
                .HasForeignKey(t => t.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);
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

            entity.Property(t => t.ArchivedDate)
                .IsRequired();

            entity.Property(t => t.CommentCount)
                .IsRequired();

            entity.Property(t => t.AttachmentCount)
                .IsRequired();

            entity.HasIndex(t => t.Status);
            entity.HasIndex(t => t.Priority);
            entity.HasIndex(t => t.ArchivedDate);

            entity.HasOne(t => t.CreatedByUser)
                .WithMany()
                .HasForeignKey(t => t.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(t => t.ArchivedByUser)
                .WithMany()
                .HasForeignKey(t => t.ArchivedBy)
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
                .HasConversion<string>();
            
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

        modelBuilder.Entity<TicketRoutingRule>(entity =>
        {
            entity.HasKey(rule => rule.Id);

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
            entity.HasIndex(rule => new { rule.Department, rule.TitleContains });
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
    }
}
