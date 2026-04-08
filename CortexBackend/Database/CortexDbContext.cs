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
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<SlaConfiguration> SlaConfigurations => Set<SlaConfiguration>();
    public DbSet<ArchiveConfiguration> ArchiveConfigurations => Set<ArchiveConfiguration>();

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

            entity.Property(configuration => configuration.ArchiveResolvedTickets)
                .IsRequired();

            entity.Property(configuration => configuration.ArchiveClosedTickets)
                .IsRequired();
        });

        modelBuilder.Entity<Comment>()
            .HasOne(c => c.CreatedByUser)
            .WithMany()
            .HasForeignKey(c => c.CreatedBy)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
