using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Cortex.API.DTO;
using Microsoft.Extensions.Options;

namespace Cortex.API.Services;

public class Auth0ManagementService(
    HttpClient httpClient,
    IOptions<Auth0ManagementOptions> options) : IAuth0ManagementService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient = httpClient;
    private readonly Auth0ManagementOptions _options = options.Value;

    public async Task<string> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var accessToken = await GetManagementTokenAsync(cancellationToken);
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            BuildUri("/api/v2/users"))
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new
                    {
                        connection = _options.DatabaseConnection,
                        email = request.Email.Trim(),
                        password = request.Password,
                        name = request.DisplayName.Trim(),
                        nickname = string.IsNullOrWhiteSpace(request.NickName)
                            ? request.DisplayName.Trim()
                            : request.NickName.Trim(),
                        blocked = !request.IsActive,
                        email_verified = false,
                        verify_email = false,
                        user_metadata = new
                        {
                            department = string.IsNullOrWhiteSpace(request.Department)
                                ? null
                                : request.Department.Trim(),
                            phone_number = string.IsNullOrWhiteSpace(request.PhoneNumber)
                                ? null
                                : request.PhoneNumber.Trim(),
                            cortex_role = request.Role.Trim()
                        }
                    },
                    JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(response, "Failed to create Auth0 user.");
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("user_id", out var userIdElement) ||
            userIdElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(userIdElement.GetString()))
        {
            throw new Auth0ManagementException("Auth0 returned an invalid user response.", 502);
        }

        return userIdElement.GetString()!;
    }

    public async Task DeleteUserAsync(
        string auth0UserId,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var accessToken = await GetManagementTokenAsync(cancellationToken);
        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            BuildUri($"/api/v2/users/{Uri.EscapeDataString(auth0UserId)}"));

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            throw await CreateExceptionAsync(response, "Failed to delete Auth0 user.");
        }
    }

    private async Task<string> GetManagementTokenAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildUri("/oauth/token"))
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new
                    {
                        client_id = _options.ManagementClientId,
                        client_secret = _options.ManagementClientSecret,
                        audience = $"https://{_options.Domain}/api/v2/",
                        grant_type = "client_credentials"
                    },
                    JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(response, "Failed to obtain Auth0 management token.");
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!document.RootElement.TryGetProperty("access_token", out var tokenElement) ||
            tokenElement.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(tokenElement.GetString()))
        {
            throw new Auth0ManagementException("Auth0 did not return a management access token.", 502);
        }

        return tokenElement.GetString()!;
    }

    private Uri BuildUri(string path)
    {
        return new($"https://{_options.Domain.Trim().TrimEnd('/')}{path}");
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.Domain) ||
            string.IsNullOrWhiteSpace(_options.ManagementClientId) ||
            string.IsNullOrWhiteSpace(_options.ManagementClientSecret) ||
            string.IsNullOrWhiteSpace(_options.DatabaseConnection))
        {
            throw new InvalidOperationException(
                "Auth0 management configuration is incomplete. Set Auth0:ManagementClientId, Auth0:ManagementClientSecret, and Auth0:DatabaseConnection.");
        }
    }

    private static async Task<Auth0ManagementException> CreateExceptionAsync(
        HttpResponseMessage response,
        string fallbackMessage)
    {
        var message = fallbackMessage;

        try
        {
            using var stream = await response.Content.ReadAsStreamAsync();
            using var document = await JsonDocument.ParseAsync(stream);

            if (document.RootElement.TryGetProperty("message", out var messageElement) &&
                messageElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(messageElement.GetString()))
            {
                message = messageElement.GetString()!;
            }
            else if (document.RootElement.TryGetProperty("error_description", out var descriptionElement) &&
                descriptionElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(descriptionElement.GetString()))
            {
                message = descriptionElement.GetString()!;
            }
            else if (document.RootElement.TryGetProperty("error", out var errorElement) &&
                errorElement.ValueKind == JsonValueKind.String &&
                !string.IsNullOrWhiteSpace(errorElement.GetString()))
            {
                message = errorElement.GetString()!;
            }
        }
        catch
        {
            // Fall back to the provided message when the response body cannot be parsed.
        }

        return new Auth0ManagementException(message, (int)response.StatusCode);
    }
}
