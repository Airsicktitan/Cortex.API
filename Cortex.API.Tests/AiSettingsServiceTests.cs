using Cortex.API.Configuration;
using Cortex.API.Data.Repositories;
using Cortex.API.Database;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace Cortex.API.Tests;

public class AiSettingsServiceTests
{
    [Fact]
    public async Task GetAsync_CreatesDefaultConfiguration_WhenMissing()
    {
        await using var context = CreateContext();
        var repository = new AiSettingsConfigurationRepository(context);
        var service = new AiSettingsService(
            repository,
            Mock.Of<IUserContextService>(),
            Options.Create(new OpenAiOptions
            {
                Model = "gpt-4.1-mini",
            }));

        var configuration = await service.GetAsync();

        Assert.True(configuration.IsIntakeAssistEnabled);
        Assert.True(configuration.IsTriageEnabled);
        Assert.True(configuration.IsScreenshotInsightEnabled);
        Assert.False(configuration.IsSuggestedUpdatesEnabled);
        Assert.Equal("gpt-4.1-mini", configuration.DefaultTextModel);
        Assert.Equal("gpt-4.1-mini", configuration.DefaultVisionModel);
        Assert.Equal(0.2, configuration.Temperature);
        Assert.Equal(1800, configuration.MaxTokens);
        Assert.Equal(120, configuration.TimeoutSeconds);
        Assert.Equal(0, configuration.RetryCount);
        Assert.Equal(0.7, configuration.ConfidenceThreshold);
        Assert.Equal(5, configuration.MaxScreenshotAttachmentCount);
        Assert.Single(context.AiSettingsConfigurations);
    }

    [Fact]
    public async Task SaveAsync_PersistsAuditMetadataAndSnapshots()
    {
        await using var context = CreateContext();
        var actingUser = new User
        {
            Id = 321,
            DisplayName = "Admin User",
            Email = "admin@example.com",
            Role = Auth0Roles.Admin,
            CreatedDate = DateTime.UtcNow,
        };
        context.Users.Add(actingUser);
        await context.SaveChangesAsync();

        var repository = new AiSettingsConfigurationRepository(context);
        var userContext = new Mock<IUserContextService>(MockBehavior.Strict);
        userContext.Setup(service => service.GetCurrentUserAsync()).ReturnsAsync(actingUser);

        var service = new AiSettingsService(
            repository,
            userContext.Object,
            Options.Create(new OpenAiOptions
            {
                Model = "gpt-4o-mini",
            }));

        var current = await service.GetAsync();
        current.IsTriageEnabled = false;
        current.DefaultTextModel = "gpt-4.1-mini";
        current.MaxTokens = 900;

        var saved = await service.SaveAsync(current);

        Assert.False(saved.IsTriageEnabled);
        Assert.Equal("gpt-4.1-mini", saved.DefaultTextModel);
        Assert.Equal(900, saved.MaxTokens);
        Assert.Equal(actingUser.Id, saved.LastModifiedBy);
        Assert.NotNull(saved.LastModifiedDateUtc);

        var auditEntry = await context.AiSettingsAuditEntries.SingleAsync();
        Assert.Equal(actingUser.Id, auditEntry.ChangedBy);
        Assert.Contains("\"isTriageEnabled\":true", auditEntry.BeforeSnapshotJson);
        Assert.Contains("\"isTriageEnabled\":false", auditEntry.AfterSnapshotJson);
        Assert.Contains("\"defaultTextModel\":\"gpt-4.1-mini\"", auditEntry.AfterSnapshotJson);

        var persisted = await context.AiSettingsConfigurations.SingleAsync();
        Assert.Equal(actingUser.Id, persisted.LastModifiedBy);
        Assert.NotNull(persisted.LastModifiedDateUtc);
    }

    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"ai-settings-{Guid.NewGuid():N}")
            .Options;

        return new CortexDbContext(options);
    }
}
