using System.Text.Json;
using Cortex.API.Configuration;
using Cortex.API.Database;
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

public class IntegrationConnectionHealthTests
{
    private static IDataProtectionProvider CreateTestProtector(string appName)
    {
        var services = new ServiceCollection();
        services.AddDataProtection().SetApplicationName(appName);
        return services.BuildServiceProvider().GetRequiredService<IDataProtectionProvider>();
    }

    private static CortexDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<CortexDbContext>()
            .UseInMemoryDatabase($"int-health-{Guid.NewGuid():N}")
            .Options;
        return new CortexDbContext(options);
    }

    private static IIntegrationCredentialAdminService CreateCredentialAdmin(CortexDbContext ctx, string protectorName)
    {
        var store = new EncryptedIntegrationCredentialStore(ctx, CreateTestProtector(protectorName));
        var activity = new IntegrationActivityService(ctx);
        var userMock = new Mock<IUserContextService>(MockBehavior.Strict);
        userMock
            .Setup(u => u.GetCurrentUserAsync())
            .ReturnsAsync(
                new User
                {
                    Id = 42,
                    DisplayName = "Cred admin",
                    Email = "cred.admin@cortex.test",
                    Role = Auth0Roles.Admin,
                    CreatedDate = DateTime.UtcNow,
                });
        return new IntegrationCredentialAdminService(
            ctx,
            store,
            activity,
            userMock.Object,
            Options.Create(new SharePointGraphOptions()),
            NullLogger<IntegrationCredentialAdminService>.Instance);
    }

    private static IIntegrationConnectionHealthService CreateHealthService(
        CortexDbContext ctx,
        FakeSharePointGraphClient graph,
        SharePointGraphOptions spo)
    {
        var activity = new IntegrationActivityService(ctx);
        var userMock = new Mock<IUserContextService>(MockBehavior.Strict);
        userMock.Setup(u => u.GetCurrentUserAsync()).ReturnsAsync(
            new User
            {
                Id = 99,
                DisplayName = "Health tester",
                Email = "health.tester@cortex.test",
                Role = Auth0Roles.Admin,
                CreatedDate = DateTime.UtcNow,
            });
        return new IntegrationConnectionHealthService(
            ctx,
            graph,
            Options.Create(spo),
            activity,
            userMock.Object,
            NullLogger<IntegrationConnectionHealthService>.Instance);
    }

    private static Dictionary<string, string?> ServiceNowMinimal() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["instanceUrl"] = "https://testinstance.service-now.com",
            ["tableName"] = "incident",
        };

    [Fact]
    public async Task GetHealth_missing_required_setting_is_NotConfigured()
    {
        await using var ctx = CreateContext();
        ctx.IntegrationConnections.Add(
            new IntegrationConnection
            {
                Provider = IntegrationProvider.Jira,
                DisplayName = "J",
                AuthMode = IntegrationAuthMode.ApiToken,
                SyncMode = IntegrationSyncMode.ReadOnly,
                IsEnabled = true,
                CreatedAtUtc = DateTime.UtcNow,
                PublicSettingsJson = JsonSerializer.Serialize(
                    new Dictionary<string, string?> { ["baseUrl"] = "https://x.atlassian.net" }),
            });
        await ctx.SaveChangesAsync();
        var id = ctx.IntegrationConnections.Single().Id;

        var health = CreateHealthService(ctx, new FakeSharePointGraphClient(), new SharePointGraphOptions());
        var dto = await health.GetHealthAsync(id);
        Assert.NotNull(dto);
        Assert.Equal(IntegrationConnectionHealthStatus.NotConfigured, dto!.Status);
        Assert.Contains("projectKey", dto.MissingRequiredSettingKeys, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Test_connection_missing_credentials_writes_activity_and_sets_last_test()
    {
        await using var ctx = CreateContext();
        var integration = IntegrationServiceTestFactory.Create(ctx);
        var conn = await integration.CreateConnectionAsync(
            new CreateIntegrationConnectionRequest
            {
                Provider = IntegrationProvider.Jira,
                DisplayName = "J",
                AuthMode = IntegrationAuthMode.ApiToken,
                ProviderSettings = IntegrationConnectionTestDefaults.JiraMinimalSettings(),
            });

        var health = CreateHealthService(ctx, new FakeSharePointGraphClient(), new SharePointGraphOptions());
        var result = await health.TestConnectionAsync(conn.Id);
        Assert.NotNull(result);
        Assert.False(result!.TestSucceeded);
        Assert.Equal(IntegrationConnectionHealthStatus.MissingCredentials, result.Health.Status);

        var row = await ctx.IntegrationConnections.AsNoTracking().SingleAsync(c => c.Id == conn.Id);
        Assert.NotNull(row.LastConnectionTestAtUtc);
        Assert.Equal(IntegrationConnectionHealthStatus.MissingCredentials.ToString(), row.LastConnectionTestHealthStatus);

        var log = await ctx.IntegrationActivityLogs.SingleAsync(l => l.ActivityType == IntegrationActivityType.ConnectionTested);
        Assert.Equal(IntegrationActivityStatus.Failed, log.Status);
        Assert.Contains("apiToken", log.MetadataJson ?? "", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret-value-never-logged", log.MetadataJson ?? "", StringComparison.Ordinal);
    }

    [Fact]
    public async Task Test_Jira_with_credentials_is_TestUnavailable_success()
    {
        await using var ctx = CreateContext();
        var integration = IntegrationServiceTestFactory.Create(ctx);
        var conn = await integration.CreateConnectionAsync(
            new CreateIntegrationConnectionRequest
            {
                Provider = IntegrationProvider.Jira,
                DisplayName = "J",
                AuthMode = IntegrationAuthMode.ApiToken,
                ProviderSettings = IntegrationConnectionTestDefaults.JiraMinimalSettings(),
            });

        var admin = CreateCredentialAdmin(ctx, "Cortex.Health.Jira.Tests");
        _ = await admin.ConfigureAsync(
            conn.Id,
            new ConfigureIntegrationCredentialRequest
            {
                Secrets = new Dictionary<string, string?> { ["apiToken"] = "supersecret-jira-token" },
            });

        var health = CreateHealthService(ctx, new FakeSharePointGraphClient(), new SharePointGraphOptions());
        var result = await health.TestConnectionAsync(conn.Id);
        Assert.NotNull(result);
        Assert.True(result!.TestSucceeded);
        Assert.Equal(IntegrationConnectionHealthStatus.TestUnavailable, result.Health.Status);
        Assert.Contains("not enabled yet", result.Health.Message, StringComparison.OrdinalIgnoreCase);

        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("supersecret", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Test_ServiceNow_with_credentials_is_TestUnavailable_success()
    {
        await using var ctx = CreateContext();
        var integration = IntegrationServiceTestFactory.Create(ctx);
        var conn = await integration.CreateConnectionAsync(
            new CreateIntegrationConnectionRequest
            {
                Provider = IntegrationProvider.ServiceNow,
                DisplayName = "SN",
                AuthMode = IntegrationAuthMode.ApiToken,
                ProviderSettings = ServiceNowMinimal(),
            });

        var admin = CreateCredentialAdmin(ctx, "Cortex.Health.Sn.Tests");
        _ = await admin.ConfigureAsync(
            conn.Id,
            new ConfigureIntegrationCredentialRequest
            {
                Secrets = new Dictionary<string, string?> { ["apiToken"] = "snc-secret-token" },
            });

        var health = CreateHealthService(ctx, new FakeSharePointGraphClient(), new SharePointGraphOptions());
        var result = await health.TestConnectionAsync(conn.Id);
        Assert.NotNull(result);
        Assert.True(result!.TestSucceeded);
        Assert.Equal(IntegrationConnectionHealthStatus.TestUnavailable, result.Health.Status);
        Assert.Contains("ServiceNow", result.Health.Message, StringComparison.OrdinalIgnoreCase);

        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("snc-secret", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Test_SapReference_message_is_metadata_only_not_live_SAP()
    {
        await using var ctx = CreateContext();
        var integration = IntegrationServiceTestFactory.Create(ctx);
        var conn = await integration.CreateConnectionAsync(
            new CreateIntegrationConnectionRequest
            {
                Provider = IntegrationProvider.SapReference,
                DisplayName = "SAP",
                AuthMode = IntegrationAuthMode.ReferenceMetadata,
                ProviderSettings = new Dictionary<string, string?>
                {
                    ["sourceName"] = "Cat",
                    ["systemAlias"] = "ECC",
                },
            });

        var health = CreateHealthService(ctx, new FakeSharePointGraphClient(), new SharePointGraphOptions());
        var result = await health.TestConnectionAsync(conn.Id);
        Assert.NotNull(result);
        Assert.True(result!.TestSucceeded);
        Assert.Contains("metadata only", result.Health.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not configured", result.Health.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Test_SharePoint_with_graph_app_runs_live_validation_Healthy()
    {
        await using var ctx = CreateContext();
        var graph = new FakeSharePointGraphClient();
        var spo = new SharePointGraphOptions
        {
            TenantId = IntegrationConnectionTestDefaults.SharePointTenantId,
            ClientId = "app-id",
            ClientSecret = "app-secret",
        };
        var integration = IntegrationServiceTestFactory.Create(ctx, graph, spo);
        var conn = await integration.CreateConnectionAsync(
            new CreateIntegrationConnectionRequest
            {
                Provider = IntegrationProvider.SharePoint,
                DisplayName = "SP",
                TenantId = IntegrationConnectionTestDefaults.SharePointTenantId,
            });

        var health = CreateHealthService(ctx, graph, spo);
        var result = await health.TestConnectionAsync(conn.Id);
        Assert.NotNull(result);
        Assert.True(result!.TestSucceeded);
        Assert.Equal(IntegrationConnectionHealthStatus.Healthy, result.Health.Status);
        Assert.Equal(IntegrationConnectionTestMode.LiveProviderValidation, result.Health.TestMode);
    }

    [Fact]
    public async Task Test_SharePoint_graph_validation_exception_is_sanitized_in_message()
    {
        await using var ctx = CreateContext();
        var graph = new FakeSharePointGraphClient
        {
            ValidateCredentialsException = new IntegrationApiException(
                401,
                "token abc123 rejected https://login.microsoft.com"),
        };
        var spo = new SharePointGraphOptions
        {
            TenantId = IntegrationConnectionTestDefaults.SharePointTenantId,
            ClientId = "app-id",
            ClientSecret = "app-secret",
        };
        var integration = IntegrationServiceTestFactory.Create(ctx, graph, spo);
        var conn = await integration.CreateConnectionAsync(
            new CreateIntegrationConnectionRequest
            {
                Provider = IntegrationProvider.SharePoint,
                DisplayName = "SP",
                TenantId = IntegrationConnectionTestDefaults.SharePointTenantId,
            });

        var health = CreateHealthService(ctx, graph, spo);
        var result = await health.TestConnectionAsync(conn.Id);
        Assert.NotNull(result);
        Assert.False(result!.TestSucceeded);
        Assert.Equal(IntegrationConnectionHealthStatus.NeedsAttention, result.Health.Status);
        Assert.DoesNotContain("abc123", result.Health.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("login.microsoft", result.Health.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Test_unknown_connection_returns_null()
    {
        await using var ctx = CreateContext();
        var health = CreateHealthService(ctx, new FakeSharePointGraphClient(), new SharePointGraphOptions());
        Assert.Null(await health.GetHealthAsync(99999));
        Assert.Null(await health.TestConnectionAsync(99999));
    }
}
