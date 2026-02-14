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
    public DbSet<User> Users => Set<User>();
    public DbSet<Comment> Comments => Set<Comment>();

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

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(u => u.Id);

            entity.Property(u => u.DisplayName)
                .IsRequired()
                .HasMaxLength(100);

            entity.Property(u => u.Email)
                .IsRequired()
                .HasMaxLength(200);

            entity.HasIndex(u => u.Email)
                .IsUnique();

            entity.Property(u => u.Role)
                .HasConversion<string>();
            
            entity.HasIndex(u => u.Auth0Id)
                .IsUnique()
                .HasFilter("[Auth0Id] IS NOT NULL")
                .IsUnique();
        });

        modelBuilder.Entity<Comment>()
            .HasOne(c => c.CreatedByUser)
            .WithMany()
            .HasForeignKey(c => c.CreatedBy)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
