namespace Cortex.API.Services;

public class Auth0ManagementOptions
{
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// JWT audience for the Cortex API (user access tokens). Not used for Management API M2M tokens.
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    public string ManagementClientId { get; set; } = string.Empty;
    public string ManagementClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Audience for Auth0 Management API client-credentials tokens (e.g. <c>https://YOUR_TENANT.us.auth0.com/api/v2/</c>).
    /// When empty, defaults to <c>https://{Domain}/api/v2/</c>.
    /// </summary>
    public string ManagementApiAudience { get; set; } = string.Empty;

    public string DatabaseConnection { get; set; } = "Username-Password-Authentication";

    /// <summary>
    /// When true, PATCHes Auth0 users with <c>app_metadata.role</c> after Cortex role changes.
    /// </summary>
    public bool EnableUserAccessSync { get; set; }
}
