using Cortex.API.Configuration;
using Cortex.API.Database;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Cortex.API.Tests;

public class CortexAutonomySettingsServiceTests
{
    [Fact]
    public async Task GetEffectiveAsync_NoStoredRow_ReturnsBoundDefaults()
    {
        await using var context = CreateContext();
        var defaults = new CortexAutonomyOptions
        {
            Enabled = false,
            ShadowMode = true,
            MinConfidence = 0.85,
        };
        var service = new CortexAutonomySettingsService(context, Options.Create(defaults));

        var effective = await service.GetEffectiveAsync();

        Assert.False(effective.Enabled);
        Assert.True(effective.ShadowMode);
        Assert.Equal(0.85, effective.MinConfidence);
    }

    [Fact]
    public async Task UpdateAsync_PartialPayload_OnlyChangesSpecifiedFields()
    {
        await using var context = CreateContext();
        var defaults = new CortexAutonomyOptions
        {
            Enabled = false,
            ShadowMode = true,
            MinConfidence = 0.85,
            RecentOverrideWindowHours = 24,
            RequireClearWinner = true,
            MinAlternativeGap = 0.08,
        };
        var service = new CortexAutonomySettingsService(context, Options.Create(defaults));

        var updated = await service.UpdateAsync(
            new UpdateCortexAutonomySettingsRequest { Enabled = true, MinConfidence = 0.92 },
            actingUserId: 17);

        Assert.True(updated.Enabled);
        Assert.True(updated.ShadowMode); // unchanged from defaults
        Assert.Equal(0.92, updated.MinConfidence);
        Assert.Equal(17, updated.LastModifiedBy);
        Assert.NotNull(updated.LastModifiedDateUtc);

        var effective = await service.GetEffectiveAsync();
        Assert.True(effective.Enabled);
        Assert.True(effective.ShadowMode);
        Assert.Equal(0.92, effective.MinConfidence);
    }

    [Fact]
    public async Task UpdateAsync_OutOfRangeConfidence_IsClamped()
    {
        await using var context = CreateContext();
        var service = new CortexAutonomySettingsService(
            context,
            Options.Create(new CortexAutonomyOptions()));

        var updated = await service.UpdateAsync(
            new UpdateCortexAutonomySettingsRequest { MinConfidence = 1.5, MinAlternativeGap = -0.1 },
            actingUserId: 1);

        Assert.Equal(1.0, updated.MinConfidence);
        Assert.Equal(0.0, updated.MinAlternativeGap);
    }

    [Fact]
    public async Task UpdateAsync_ReusesExistingRow()
    {
        var dbName = $"autonomy-settings-{Guid.NewGuid():N}";

        await using (var ctx = CreateContext(dbName))
        {
            var service = new CortexAutonomySettingsService(
                ctx,
                Options.Create(new CortexAutonomyOptions()));
            await service.UpdateAsync(
                new UpdateCortexAutonomySettingsRequest { Enabled = true },
                actingUserId: 1);
        }

        await using (var ctx = CreateContext(dbName))
        {
            var service = new CortexAutonomySettingsService(
                ctx,
                Options.Create(new CortexAutonomyOptions()));
            await service.UpdateAsync(
                new UpdateCortexAutonomySettingsRequest { ShadowMode = false },
                actingUserId: 1);
        }

        await using var verifyCtx = CreateContext(dbName);
        Assert.Single(verifyCtx.CortexAutonomyConfigurations);
    }

    private static CortexDbContext CreateContext(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase(dbName ?? $"autonomy-settings-{Guid.NewGuid():N}")
            .Options;
        return new CortexDbContext(options);
    }
}
