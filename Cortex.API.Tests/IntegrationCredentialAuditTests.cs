using System.Text.Json;
using Cortex.API.Database;
using Cortex.API.Configuration;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Cortex.API.Services.Integrations;
using Cortex.API.Tests.TestDoubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace Cortex.API.Tests;

public class IntegrationCredentialAuditTests
{
    private static IDataProtectionProvider CreateTestProtector()
    {
        var services = new ServiceCollection();
        services.AddDataProtection().SetApplicationName("Cortex.IntegrationCredentialAudit.Tests");
        return services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
    }

    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"int-cred-audit-{Guid.NewGuid():N}")
            .Options;
        return new CortexDbContext(options);
    }

    private static IIntegrationCredentialAdminService CreateAdmin(CortexDbContext ctx, SharePointGraphOptions? spo = null)
    {
        var store = new EncryptedIntegrationCredentialStore(ctx, CreateTestProtector());
        var activity = new IntegrationActivityService(ctx);
        var userMock = new Mock<IUserContextService>(MockBehavior.Strict);
        userMock
            .Setup(u => u.GetCurrentUserAsync())
            .ReturnsAsync(
                new User
                {
                    Id = 7,
                    DisplayName = "Credential admin",
                    Email = "cred.admin@cortex.test",
                    Role = Auth0Roles.Admin,
                    CreatedDate = DateTime.UtcNow,
                });
        return new IntegrationCredentialAdminService(
            ctx,
            store,
            activity,
            userMock.Object,
            Options.Create(spo ?? new SharePointGraphOptions()),
            NullLogger<IntegrationCredentialAdminService>.Instance);
    }

    [Fact]
    public async Task Configure_first_time_writes_CredentialConfigured_without_secret_in_metadata()
    {
        await using var ctx = CreateContext();
        var integration = IntegrationServiceTestFactory.Create(ctx, graphOptions: new SharePointGraphOptions());
        var conn = await integration.CreateConnectionAsync(
            new CreateIntegrationConnectionRequest
            {
                Provider = IntegrationProvider.Jira,
                DisplayName = "J audit",
                ProviderSettings = IntegrationConnectionTestDefaults.JiraMinimalSettings(),
            });

        var admin = CreateAdmin(ctx);
        _ = await admin.ConfigureAsync(
            conn.Id,
            new ConfigureIntegrationCredentialRequest
            {
                Secrets = new Dictionary<string, string?> { ["apiToken"] = "supersecret-token-xyz" },
            });

        var log = await ctx.IntegrationActivityLogs.SingleAsync();
        Assert.Equal(IntegrationActivityType.CredentialConfigured, log.ActivityType);
        Assert.Equal(IntegrationActivityStatus.Success, log.Status);
        Assert.Null(log.ExternalWorkSourceId);
        Assert.Equal(conn.Id, log.IntegrationConnectionId);
        Assert.Equal("Credential configured", log.Message);
        Assert.False(string.IsNullOrWhiteSpace(log.MetadataJson));
        Assert.DoesNotContain("supersecret", log.MetadataJson, StringComparison.Ordinal);
        Assert.DoesNotContain("token-xyz", log.MetadataJson, StringComparison.Ordinal);
        Assert.Contains("apiToken", log.MetadataJson, StringComparison.OrdinalIgnoreCase);

        using var meta = JsonDocument.Parse(log.MetadataJson!);
        Assert.Equal(conn.Id, meta.RootElement.GetProperty("connectionId").GetInt32());
        Assert.True(meta.RootElement.GetProperty("credentialConfigured").GetBoolean());
    }

    [Fact]
    public async Task Configure_second_time_writes_CredentialRotated()
    {
        await using var ctx = CreateContext();
        var integration = IntegrationServiceTestFactory.Create(ctx, graphOptions: new SharePointGraphOptions());
        var conn = await integration.CreateConnectionAsync(
            new CreateIntegrationConnectionRequest
            {
                Provider = IntegrationProvider.Jira,
                DisplayName = "J",
                ProviderSettings = IntegrationConnectionTestDefaults.JiraMinimalSettings(),
            });

        var admin = CreateAdmin(ctx);
        _ = await admin.ConfigureAsync(
            conn.Id,
            new ConfigureIntegrationCredentialRequest
            {
                Secrets = new Dictionary<string, string?> { ["apiToken"] = "first" },
            });
        _ = await admin.ConfigureAsync(
            conn.Id,
            new ConfigureIntegrationCredentialRequest
            {
                Secrets = new Dictionary<string, string?> { ["apiToken"] = "second-value-never-logged" },
            });

        var rotated = await ctx.IntegrationActivityLogs
            .AsNoTracking()
            .OrderByDescending(a => a.Id)
            .FirstAsync();
        Assert.Equal(IntegrationActivityType.CredentialRotated, rotated.ActivityType);
        Assert.Equal("Credential rotated", rotated.Message);
        Assert.DoesNotContain("second-value", rotated.MetadataJson ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Clear_writes_CredentialCleared()
    {
        await using var ctx = CreateContext();
        var integration = IntegrationServiceTestFactory.Create(ctx, graphOptions: new SharePointGraphOptions());
        var conn = await integration.CreateConnectionAsync(
            new CreateIntegrationConnectionRequest
            {
                Provider = IntegrationProvider.Jira,
                DisplayName = "J",
                ProviderSettings = IntegrationConnectionTestDefaults.JiraMinimalSettings(),
            });

        var admin = CreateAdmin(ctx);
        _ = await admin.ConfigureAsync(
            conn.Id,
            new ConfigureIntegrationCredentialRequest
            {
                Secrets = new Dictionary<string, string?> { ["apiToken"] = "t" },
            });

        ctx.IntegrationActivityLogs.RemoveRange(ctx.IntegrationActivityLogs);
        await ctx.SaveChangesAsync();

        _ = await admin.ClearAsync(conn.Id);

        var log = await ctx.IntegrationActivityLogs.SingleAsync();
        Assert.Equal(IntegrationActivityType.CredentialCleared, log.ActivityType);
        Assert.Equal("Credential cleared", log.Message);
        using var meta = JsonDocument.Parse(log.MetadataJson ?? "{}");
        Assert.False(meta.RootElement.GetProperty("credentialConfigured").GetBoolean());
    }

    [Fact]
    public async Task Configure_unknown_key_does_not_write_credential_activity()
    {
        await using var ctx = CreateContext();
        var integration = IntegrationServiceTestFactory.Create(ctx, graphOptions: new SharePointGraphOptions());
        var conn = await integration.CreateConnectionAsync(
            new CreateIntegrationConnectionRequest
            {
                Provider = IntegrationProvider.Jira,
                DisplayName = "J",
                ProviderSettings = IntegrationConnectionTestDefaults.JiraMinimalSettings(),
            });

        var admin = CreateAdmin(ctx);
        _ = await Assert.ThrowsAsync<IntegrationApiException>(() =>
            admin.ConfigureAsync(
                conn.Id,
                new ConfigureIntegrationCredentialRequest
                {
                    Secrets = new Dictionary<string, string?> { ["badKey"] = "v" },
                }));

        Assert.Empty(await ctx.IntegrationActivityLogs.ToListAsync());
    }

    [Fact]
    public async Task Create_connection_with_secret_in_ProviderSettings_does_not_write_credential_audit()
    {
        await using var ctx = CreateContext();
        var integration = IntegrationServiceTestFactory.Create(ctx, graphOptions: new SharePointGraphOptions());
        _ = await Assert.ThrowsAsync<IntegrationApiException>(() =>
            integration.CreateConnectionAsync(
                new CreateIntegrationConnectionRequest
                {
                    Provider = IntegrationProvider.Jira,
                    DisplayName = "J",
                    ProviderSettings = new Dictionary<string, string?>(
                        IntegrationConnectionTestDefaults.JiraMinimalSettings())
                    {
                        ["apiToken"] = "should-not-be-stored",
                    },
                }));

        Assert.Empty(await ctx.IntegrationActivityLogs.ToListAsync());
    }

    [Fact]
    public async Task GetConnectionActivity_includes_source_scoped_and_connection_scoped_rows()
    {
        await using var ctx = CreateContext();
        var integration = IntegrationServiceTestFactory.Create(ctx, graphOptions: new SharePointGraphOptions());
        var conn = await integration.CreateConnectionAsync(
            new CreateIntegrationConnectionRequest
            {
                Provider = IntegrationProvider.Jira,
                DisplayName = "J",
                ProviderSettings = IntegrationConnectionTestDefaults.JiraMinimalSettings(),
            });
        var source = await integration.CreateSourceAsync(
            conn.Id,
            new CreateExternalWorkSourceRequest
            {
                Provider = IntegrationProvider.Jira,
                SourceType = ExternalSourceType.JiraProject,
                ExternalSourceId = "P1",
                Name = "Board",
            });

        var activity = new IntegrationActivityService(ctx);
        var t = DateTime.UtcNow;
        await activity.RecordAsync(
            new IntegrationActivityLogRecordRequest
            {
                ExternalWorkSourceId = source!.Id,
                ActivityType = IntegrationActivityType.ManualUpsert,
                Status = IntegrationActivityStatus.Success,
                StartedAtUtc = t,
                CompletedAtUtc = t,
                Message = "source scoped",
            });
        await activity.RecordAsync(
            new IntegrationActivityLogRecordRequest
            {
                ExternalWorkSourceId = null,
                IntegrationConnectionId = conn.Id,
                ActivityType = IntegrationActivityType.CredentialConfigured,
                Status = IntegrationActivityStatus.Success,
                StartedAtUtc = t.AddMinutes(1),
                CompletedAtUtc = t.AddMinutes(1),
                Message = "Credential configured",
            });

        var rows = await activity.GetConnectionActivityAsync(conn.Id, 20, null);
        Assert.NotNull(rows);
        Assert.Equal(2, rows!.Count);
        Assert.Contains(rows, r => r.ActivityType == IntegrationActivityType.CredentialConfigured);
        Assert.Contains(rows, r => r.ActivityType == IntegrationActivityType.ManualUpsert);
    }
}
