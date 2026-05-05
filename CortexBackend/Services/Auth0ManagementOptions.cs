namespace Cortex.API.Services;

public class Auth0ManagementOptions
{
    /// <summary>
    /// Auth0 Domain (tenant hostname), for example <c>dev-xxxxx.us.auth0.com</c>.
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>Management API M2M client id (often labeled Client ID in Auth0).</summary>
    public string ManagementClientId { get; set; } = string.Empty;

    /// <summary>Management API client secret — store in secrets, never commit.</summary>
    public string ManagementClientSecret { get; set; } = string.Empty;


    /// <summary>
    /// Audience for Auth0 Management API M2M tokens (e.g. <c>https://YOUR_TENANT.us.auth0.com/api/v2/</c>).
    /// When empty, derived from <see cref="Domain"/> as <c>https://{Domain}/api/v2/</c>.
    /// </summary>
    public string ManagementApiAudience { get; set; } = string.Empty;

    public string DatabaseConnection { get; set; } = "Username-Password-Authentication";

    /// <summary>
    /// When true, Cortex POSTs PATCH /api/v2/users/&lt;id&gt; after local profile nickname/display saves.
    /// Requires management client credentials with <c>update:users</c> scope.
    /// </summary>
    public bool EnableProfileWriteBack { get; set; }

    /// <summary>
    /// When true, PATCHes Auth0 users with <c>app_metadata.role</c> after Cortex role changes.
    /// </summary>
    public bool EnableUserAccessSync { get; set; }

    /// <summary>True when domain + M2M client id + secret can call the Management API.</summary>
    public bool IsManagementApiClientConfigured =>
        !string.IsNullOrWhiteSpace(Domain) &&
        !string.IsNullOrWhiteSpace(ManagementClientId) &&
        !string.IsNullOrWhiteSpace(ManagementClientSecret);

    /// <summary>
    /// Returns the resolved Management API audience URL, normalised with a trailing slash.
    /// Uses <see cref="ManagementApiAudience"/> when set; otherwise derives from <see cref="Domain"/>.
    /// Callers are responsible for ensuring <see cref="Domain"/> is non-empty before calling.
    /// </summary>
    public string ResolveManagementApiAudience()
    {
        if (!string.IsNullOrWhiteSpace(ManagementApiAudience))
        {
            var trimmed = ManagementApiAudience.Trim();
            return trimmed.EndsWith('/') ? trimmed : trimmed + "/";
        }

        var domain = Domain?.Trim().TrimEnd('/');
        return $"https://{domain}/api/v2/";
    }
}
