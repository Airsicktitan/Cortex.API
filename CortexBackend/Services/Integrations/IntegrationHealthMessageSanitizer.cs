namespace Cortex.API.Services.Integrations;

/// <summary>Maps exceptions to enterprise-safe connection test messages (no tokens or URLs from providers).</summary>
public static class IntegrationHealthMessageSanitizer
{
    public static string SanitizeForConnectionTest(Exception ex)
    {
        if (ex is IntegrationApiException ia)
        {
            return ia.StatusCode switch
            {
                401 or 403 => ProviderAuthFailureMessage,
                404 => ProviderNotFoundMessage,
                409 => "The provider rejected the request due to a conflict. Review settings and try again.",
                502 => ProviderAuthFailureMessage,
                _ => NeedsAttentionMessage,
            };
        }

        return NeedsAttentionMessage;
    }

    public const string NeedsAttentionMessage =
        "Connection test failed. Review the provider settings and credentials.";

    public const string ProviderAuthFailureMessage =
        "Provider authentication failed. Rotate credentials or verify provider permissions.";

    public const string ProviderNotFoundMessage =
        "Provider resource was not found or is not accessible with the configured permissions.";
}
