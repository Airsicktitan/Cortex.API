namespace Cortex.API.Tests;

/// <summary>Stable identifiers and minimal provider settings for integration tests.</summary>
public static class IntegrationConnectionTestDefaults
{
    public const string SharePointTenantId = "00000000-0000-0000-0000-000000000001";

    public static Dictionary<string, string?> JiraMinimalSettings() => new(StringComparer.OrdinalIgnoreCase)
    {
        ["baseUrl"] = "https://test.atlassian.net",
        ["projectKey"] = "PROJ",
        ["issueType"] = "Task",
    };
}
