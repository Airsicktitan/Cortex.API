namespace Cortex.API.Services;

public class Auth0ManagementOptions
{
    public string Domain { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string ManagementClientId { get; set; } = string.Empty;
    public string ManagementClientSecret { get; set; } = string.Empty;
    public string DatabaseConnection { get; set; } = "Username-Password-Authentication";

    /// <summary>
    /// Enables syncing Cortex user role and permissions to Auth0 Management API.
    /// Defaults to false to preserve current behavior when not configured.
    /// </summary>
    public bool EnableUserAccessSync { get; set; }

    /// <summary>
    /// Resource server identifier used when assigning user permissions.
    /// Falls back to <see cref="Audience"/> when omitted.
    /// </summary>
    public string? ManagementPermissionAudience { get; set; }
}
