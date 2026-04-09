namespace Cortex.API.Services;

public class Auth0ManagementOptions
{
    public string Domain { get; set; } = string.Empty;
    public string ManagementClientId { get; set; } = string.Empty;
    public string ManagementClientSecret { get; set; } = string.Empty;
    public string DatabaseConnection { get; set; } = "Username-Password-Authentication";
}
