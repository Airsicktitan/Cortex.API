using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services.Integrations;

namespace Cortex.API.Tests;

public class IntegrationConnectionConfigValidatorTests
{
    [Fact]
    public void Create_SharePoint_throws_when_tenant_missing()
    {
        var ex = Assert.Throws<IntegrationApiException>(() =>
            IntegrationConnectionConfigValidator.ValidateAndNormalizeCreate(
                new CreateIntegrationConnectionRequest
                {
                    Provider = IntegrationProvider.SharePoint,
                    DisplayName = "S",
                    AuthMode = IntegrationAuthMode.Manual,
                }));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void Create_Jira_throws_on_invalid_sync_mode()
    {
        var ex = Assert.Throws<IntegrationApiException>(() =>
            IntegrationConnectionConfigValidator.ValidateAndNormalizeCreate(
                new CreateIntegrationConnectionRequest
                {
                    Provider = IntegrationProvider.Jira,
                    DisplayName = "J",
                    AuthMode = IntegrationAuthMode.ApiToken,
                    SyncMode = IntegrationSyncMode.TwoWay,
                    ProviderSettings = IntegrationConnectionTestDefaults.JiraMinimalSettings(),
                }));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void Create_rejects_unknown_provider_settings_key()
    {
        var d = IntegrationConnectionTestDefaults.JiraMinimalSettings();
        d["madeUpKey"] = "x";
        var ex = Assert.Throws<IntegrationApiException>(() =>
            IntegrationConnectionConfigValidator.ValidateAndNormalizeCreate(
                new CreateIntegrationConnectionRequest
                {
                    Provider = IntegrationProvider.Jira,
                    DisplayName = "J",
                    ProviderSettings = d,
                }));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void Create_rejects_non_empty_secret_field_values()
    {
        var ex = Assert.Throws<IntegrationApiException>(() =>
            IntegrationConnectionConfigValidator.ValidateAndNormalizeCreate(
                new CreateIntegrationConnectionRequest
                {
                    Provider = IntegrationProvider.Jira,
                    DisplayName = "J",
                    ProviderSettings = new Dictionary<string, string?>
                    {
                        ["baseUrl"] = "https://test.atlassian.net",
                        ["projectKey"] = "P",
                        ["issueType"] = "Task",
                        ["apiToken"] = "secret-token",
                    },
                }));
        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public void ToSafeDisplayMap_omits_secret_keys_from_public_json()
    {
        var profile = IntegrationProviderCatalog.Get(IntegrationProvider.Jira);
        var conn = new IntegrationConnection
        {
            Provider = IntegrationProvider.Jira,
            TenantId = null,
            OrganizationId = null,
            PublicSettingsJson = """{"baseUrl":"https://x.atlassian.net","projectKey":"Q","issueType":"Task","apiToken":"nope"}""",
        };
        var map = IntegrationConnectionConfigValidator.ToSafeDisplayMap(conn, profile);
        Assert.False(map.ContainsKey("apiToken"));
        Assert.Equal("https://x.atlassian.net", map["baseUrl"]);
        Assert.Equal("Q", map["projectKey"]);
    }
}
