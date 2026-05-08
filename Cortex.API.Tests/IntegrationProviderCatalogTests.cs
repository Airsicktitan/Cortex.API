using Cortex.API.Models;
using Cortex.API.Services.Integrations;

namespace Cortex.API.Tests;

public class IntegrationProviderCatalogTests
{
    [Fact]
    public void Catalog_lists_SharePoint_Jira_ServiceNow_SapReference()
    {
        var actual = IntegrationProviderCatalog.All.Select(p => p.Provider).ToHashSet();
        Assert.Equal(
            new HashSet<IntegrationProvider>
            {
                IntegrationProvider.SharePoint,
                IntegrationProvider.Jira,
                IntegrationProvider.ServiceNow,
                IntegrationProvider.SapReference,
            },
            actual);
    }

    [Theory]
    [InlineData(IntegrationProvider.SharePoint, "tenantId", false)]
    [InlineData(IntegrationProvider.SharePoint, "clientSecret", true)]
    [InlineData(IntegrationProvider.Jira, "apiToken", true)]
    [InlineData(IntegrationProvider.ServiceNow, "clientSecret", true)]
    [InlineData(IntegrationProvider.ServiceNow, "apiToken", true)]
    [InlineData(IntegrationProvider.SapReference, "sourceName", false)]
    public void Field_secret_flags_match_expectations(
        IntegrationProvider provider,
        string fieldKey,
        bool expectSecret)
    {
        var profile = IntegrationProviderCatalog.Get(provider);
        var field = profile.Fields.Single(f =>
            f.Key.Equals(fieldKey, StringComparison.OrdinalIgnoreCase));
        Assert.Equal(expectSecret, field.IsSecret);
    }

    [Fact]
    public void Each_catalog_provider_has_at_least_one_required_non_secret_field()
    {
        foreach (var profile in IntegrationProviderCatalog.All)
        {
            var required = profile.Fields.Where(f => f.Required).ToList();
            Assert.NotEmpty(required);
            Assert.Contains(required, f => !f.IsSecret);
        }
    }
}
