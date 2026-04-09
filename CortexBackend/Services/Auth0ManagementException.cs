namespace Cortex.API.Services;

public class Auth0ManagementException(string message, int statusCode)
    : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}
