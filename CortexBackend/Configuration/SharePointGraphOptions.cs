namespace Cortex.API.Configuration;

public sealed class SharePointGraphOptions
{
    public const string SectionName = "SharePointGraph";

    /// <summary>Optional; falls back to IntegrationConnection.TenantId when resolving tokens.</summary>
    public string? TenantId { get; set; }

    public string? ClientId { get; set; }
    public string? ClientSecret { get; set; }

    public string GraphBaseUrl { get; set; } = "https://graph.microsoft.com/v1.0";

    /// <summary>Override token endpoint; default https://login.microsoftonline.com/{tenant}/oauth2/v2.0/token</summary>
    public string? TokenUrl { get; set; }
}
