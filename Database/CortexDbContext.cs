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
        });
    }
}
