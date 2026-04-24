using Cortex.API.Services;

namespace Cortex.API.Tests;

public class ReportSourceRegistryTests
{
    [Fact]
    public void GenerateSql_OpenTickets_SynitiOwner_IncludesUserResolutionApply()
    {
        var sql = ReportSourceRegistry.GenerateSql("open_tickets", "syniti_owner");

        Assert.Contains("OUTER APPLY", sql, StringComparison.Ordinal);
        Assert.Contains("FROM Users", sql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cortex_so.DisplayName", sql, StringComparison.Ordinal);
        Assert.Contains("AS [Syniti Owner]", sql, StringComparison.Ordinal);
        Assert.Contains("WHERE t.Status NOT IN", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateSql_OmitsOwnerApplies_WhenOwnerColumnsNotSelected()
    {
        var sql = ReportSourceRegistry.GenerateSql("tickets", "id,title");

        Assert.DoesNotContain("cortex_so", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("cortex_bo", sql, StringComparison.Ordinal);
    }
}
