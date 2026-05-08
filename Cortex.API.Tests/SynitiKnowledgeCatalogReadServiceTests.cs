using Cortex.API.Database;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Tests;

public class SynitiKnowledgeCatalogReadServiceTests
{
    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"syniti-catalog-read-{Guid.NewGuid():N}")
            .Options;
        return new CortexDbContext(options);
    }

    [Fact]
    public async Task ListAsync_ReturnsEntries_OrderedByTerm()
    {
        await using var db = CreateContext();
        var now = DateTime.UtcNow;
        var src = new SynitiKnowledgeSource
        {
            Name = "Test source",
            SourceType = SynitiKnowledgeSourceType.Manual,
            IsEnabled = true,
            CreatedAtUtc = now,
        };
        db.SynitiKnowledgeSources.Add(src);
        await db.SaveChangesAsync();

        db.SynitiKnowledgeEntries.Add(new SynitiKnowledgeEntry
        {
            SynitiKnowledgeSourceId = src.Id,
            Term = "Zebra",
            Category = SynitiKnowledgeCategory.Platform,
            ShortDefinition = "Z",
            CreatedAtUtc = now,
        });
        db.SynitiKnowledgeEntries.Add(new SynitiKnowledgeEntry
        {
            SynitiKnowledgeSourceId = src.Id,
            Term = "Alpha",
            Category = SynitiKnowledgeCategory.Mapping,
            ShortDefinition = "A",
            BusinessMeaning = "Business",
            SuggestedReviewerChecks = "Check one|Check two",
            CreatedAtUtc = now,
        });
        await db.SaveChangesAsync();

        var svc = new SynitiKnowledgeCatalogReadService(db);
        var result = await svc.ListAsync(null, null);

        Assert.Equal(2, result.Entries.Count);
        Assert.Equal("Alpha", result.Entries[0].Term);
        Assert.Equal("Zebra", result.Entries[1].Term);
        Assert.Equal(2, result.Entries[0].SuggestedReviewerChecks.Count);
    }

    [Fact]
    public async Task ListAsync_Search_Filters()
    {
        await using var db = CreateContext();
        var now = DateTime.UtcNow;
        var src = new SynitiKnowledgeSource
        {
            Name = "Src",
            IsEnabled = true,
            CreatedAtUtc = now,
        };
        db.SynitiKnowledgeSources.Add(src);
        await db.SaveChangesAsync();
        db.SynitiKnowledgeEntries.Add(new SynitiKnowledgeEntry
        {
            SynitiKnowledgeSourceId = src.Id,
            Term = "Reconciliation",
            Category = SynitiKnowledgeCategory.Reconciliation,
            ShortDefinition = "Compare counts",
            CreatedAtUtc = now,
        });
        db.SynitiKnowledgeEntries.Add(new SynitiKnowledgeEntry
        {
            SynitiKnowledgeSourceId = src.Id,
            Term = "Wave",
            Category = SynitiKnowledgeCategory.Migration,
            ShortDefinition = "Slice",
            CreatedAtUtc = now,
        });
        await db.SaveChangesAsync();

        var svc = new SynitiKnowledgeCatalogReadService(db);
        var result = await svc.ListAsync("recon", null);

        Assert.Single(result.Entries);
        Assert.Equal("Reconciliation", result.Entries[0].Term);
    }

    [Fact]
    public async Task ListAsync_CategoryFilter_Filters()
    {
        await using var db = CreateContext();
        var now = DateTime.UtcNow;
        var src = new SynitiKnowledgeSource
        {
            Name = "Src",
            IsEnabled = true,
            CreatedAtUtc = now,
        };
        db.SynitiKnowledgeSources.Add(src);
        await db.SaveChangesAsync();
        db.SynitiKnowledgeEntries.Add(new SynitiKnowledgeEntry
        {
            SynitiKnowledgeSourceId = src.Id,
            Term = "R1",
            Category = SynitiKnowledgeCategory.Reconciliation,
            ShortDefinition = "x",
            CreatedAtUtc = now,
        });
        db.SynitiKnowledgeEntries.Add(new SynitiKnowledgeEntry
        {
            SynitiKnowledgeSourceId = src.Id,
            Term = "W1",
            Category = SynitiKnowledgeCategory.Migration,
            ShortDefinition = "y",
            CreatedAtUtc = now,
        });
        await db.SaveChangesAsync();

        var svc = new SynitiKnowledgeCatalogReadService(db);
        var result = await svc.ListAsync(null, "Migration");

        Assert.Single(result.Entries);
        Assert.Equal("W1", result.Entries[0].Term);
    }
}
