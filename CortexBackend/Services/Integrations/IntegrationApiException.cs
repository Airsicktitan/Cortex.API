namespace Cortex.API.Services.Integrations;

/// <summary>
/// Maps to HTTP error responses. Message must be safe for clients (no secrets, tokens, or raw auth payloads).
/// </summary>
public sealed class IntegrationApiException : Exception
{
    public int StatusCode { get; }

    public IntegrationApiException(int statusCode, string message)
        : base(message)
    {
        StatusCode = statusCode;
    }
}
