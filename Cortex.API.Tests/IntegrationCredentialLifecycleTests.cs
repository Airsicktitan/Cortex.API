using System.Text;
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

public class IntegrationCredentialLifecycleTests
{
    private static IDataProtectionProvider CreateTestProtector()
    {
        var services = new ServiceCollection();
        services.AddDataProtection().SetApplicationName("Cortex.IntegrationCredential.Tests");
        return services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
    }

    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"int-cred-{Guid.NewGuid():N}")
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
                    Id = 42,
                    DisplayName = "Audit actor",
                    Email = "audit.actor@cortex.test",
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
    public async Task Configure_Jira_token_status_never_contains_secret()
    {
        await using var ctx = CreateContext();
        var integration = IntegrationServiceTestFactory.Create(
            ctx,
            graphOptions: new SharePointGraphOptions());

        var conn = await integration.CreateConnectionAsync(
            new CreateIntegrationConnectionRequest
            {
                Provider = IntegrationProvider.Jira,
                DisplayName = "J",
                ProviderSettings = IntegrationConnectionTestDefaults.JiraMinimalSettings(),
            });

        var admin = CreateAdmin(ctx);
        var result = await admin.ConfigureAsync(
            conn.Id,
            new ConfigureIntegrationCredentialRequest
            {
                Secrets = new Dictionary<string, string?> { ["apiToken"] = "supersecret-value-do-not-leak" },
            });

        Assert.NotNull(result);
        Assert.True(result!.Status.CredentialConfigured);
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("supersecret", json, StringComparison.Ordinal);

        var row = await ctx.IntegrationConnectionCredentials.SingleAsync();
        Assert.False(Encoding.UTF8.GetString(row.ProtectedPayload).Contains("supersecret", StringComparison.Ordinal));

        var hydrated = await integration.GetConnectionAsync(conn.Id);
        Assert.NotNull(hydrated);
        Assert.True(hydrated!.CredentialConfigured);
        Assert.Contains("API token", string.Join(',', hydrated.ConfiguredCredentialFieldLabels), StringComparison.OrdinalIgnoreCase);
        foreach (var v in hydrated.SafeProviderSettings.Values)
        {
            Assert.DoesNotContain("supersecret", v, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Configure_unknown_key_throws_safe_error()
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
        var ex = await Assert.ThrowsAsync<IntegrationApiException>(() =>
            admin.ConfigureAsync(
                conn.Id,
                new ConfigureIntegrationCredentialRequest
                {
                    Secrets = new Dictionary<string, string?> { ["hackerField"] = "x" },
                }));
        Assert.Equal(400, ex.StatusCode);
        Assert.DoesNotContain("x", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Clear_removes_credential_row()
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
            new ConfigureIntegrationCredentialRequest { Secrets = new Dictionary<string, string?> { ["apiToken"] = "t" } });

        var cleared = await admin.ClearAsync(conn.Id);
        Assert.NotNull(cleared);
        Assert.False(cleared!.Status.CredentialConfigured);
        Assert.Empty(await ctx.IntegrationConnectionCredentials.ToListAsync());
    }

    [Fact]
    public async Task SapReference_configure_rejected()
    {
        await using var ctx = CreateContext();
        var integration = IntegrationServiceTestFactory.Create(ctx, graphOptions: new SharePointGraphOptions());
        var conn = await integration.CreateConnectionAsync(
            new CreateIntegrationConnectionRequest
            {
                Provider = IntegrationProvider.SapReference,
                DisplayName = "S",
                AuthMode = IntegrationAuthMode.ReferenceMetadata,
                ProviderSettings = new Dictionary<string, string?>
                {
                    ["sourceName"] = "Cat",
                    ["systemAlias"] = "ECC",
                },
            });

        var admin = CreateAdmin(ctx);
        await Assert.ThrowsAsync<IntegrationApiException>(() =>
            admin.ConfigureAsync(
                conn.Id,
                new ConfigureIntegrationCredentialRequest
                {
                    Secrets = new Dictionary<string, string?> { ["nope"] = "v" },
                }));
    }

    [Fact]
    public async Task Store_round_trips_decrypted_server_side_only()
    {
        await using var ctx = CreateContext();
        var store = new EncryptedIntegrationCredentialStore(ctx, CreateTestProtector());
        ctx.IntegrationConnections.Add(
            new IntegrationConnection
            {
                Provider = IntegrationProvider.Jira,
                DisplayName = "J",
                AuthMode = IntegrationAuthMode.ApiToken,
                SyncMode = IntegrationSyncMode.ReadOnly,
                IsEnabled = true,
                CreatedAtUtc = DateTime.UtcNow,
            });
        await ctx.SaveChangesAsync();
        var id = ctx.IntegrationConnections.Single().Id;

        await store.MergeAndPersistAsync(
            id,
            IntegrationProvider.Jira,
            IntegrationAuthMode.ApiToken,
            new Dictionary<string, string> { ["apiToken"] = "roundtrip-test" },
            default);

        var decrypted = await store.GetDecryptedSecretsAsync(id);
        Assert.NotNull(decrypted);
        Assert.Equal("roundtrip-test", decrypted!["apiToken"]);
    }
}
