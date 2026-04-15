namespace Cortex.API.Services;

public class Auth0ManagementOptions
{
    public string Domain { get; set; } = string.Empty;

    public string ManagementClientId { get; set; } = string.Empty;
    public string ManagementClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Audience for Auth0 Management API M2M tokens (e.g. <c>https://YOUR_TENANT.us.auth0.com/api/v2/</c>).
    /// When empty, derived from <see cref="Domain"/> as <c>https://{Domain}/api/v2/</c>.
    /// </summary>
    public string ManagementApiAudience { get; set; } = string.Empty;

    public string DatabaseConnection { get; set; } = "Username-Password-Authentication";

    /// <summary>
    /// When true, PATCHes Auth0 users with <c>app_metadata.role</c> after Cortex role changes.
    /// </summary>
    public bool EnableUserAccessSync { get; set; }

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
