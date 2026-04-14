using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Cortex.API.Models;
using Microsoft.Extensions.Options;

namespace Cortex.API.Services;

public class Auth0UserAccessSyncService(
    HttpClient httpClient,
    IOptions<Auth0ManagementOptions> options,
    ILogger<Auth0UserAccessSyncService> logger) : IUserAccessSyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient = httpClient;
    private readonly Auth0ManagementOptions _options = options.Value;
    private readonly ILogger<Auth0UserAccessSyncService> _logger = logger;

    public async Task QueueUserAccessSyncAsync(
        User user,
        IReadOnlyList<string> requestedPermissions,
        CancellationToken cancellationToken = default)
    {
        if (!_options.EnableUserAccessSync)
        {
            _logger.LogInformation(
                "Auth0 access sync disabled. Skipping sync for user {UserId}.",
                user.Id);
            return;
        }

        if (string.IsNullOrWhiteSpace(user.Auth0Id))
        {
            _logger.LogWarning(
                "Auth0 access sync skipped for user {UserId}: Auth0Id is missing.",
                user.Id);
            return;
        }

        if (!IsConfiguredForSync())
        {
            _logger.LogWarning(
                "Auth0 access sync skipped for user {UserId}: management configuration incomplete.",
                user.Id);
            return;
        }

        try
        {
            var accessToken = await GetManagementTokenAsync(cancellationToken);
            await SyncUserMetadataRoleAsync(user, accessToken, cancellationToken);
            await SyncPermissionsAsync(user.Auth0Id, requestedPermissions, accessToken, cancellationToken);

            _logger.LogInformation(
                "Auth0 access sync completed for user {UserId} ({Auth0UserId}).",
                user.Id,
                user.Auth0Id);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(
                exception,
                "Auth0 access sync failed for user {UserId} ({Auth0UserId}).",
                user.Id,
                user.Auth0Id);
        }
    }

    private async Task SyncUserMetadataRoleAsync(
        User user,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            new HttpMethod("PATCH"),
            BuildPath($"/api/v2/users/{Uri.EscapeDataString(user.Auth0Id!)}"))
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new
                    {
                        user_metadata = new
                        {
                            cortex_role = user.Role.ToString()
                        }
                    },
                    JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(response, "Failed to update Auth0 user metadata.");
        }
    }

    private async Task SyncPermissionsAsync(
        string auth0UserId,
        IReadOnlyList<string> requestedPermissions,
        string accessToken,
        CancellationToken cancellationToken)
    {
        var audience = ResolvePermissionAudience();
        var existingPermissions = await GetCurrentPermissionsAsync(auth0UserId, accessToken, cancellationToken);
        var existingForAudience = existingPermissions
            .Where(permission => string.Equals(permission.ResourceServerIdentifier, audience, StringComparison.OrdinalIgnoreCase))
            .Select(permission => permission.PermissionName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var targetPermissions = requestedPermissions
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var toRemove = existingForAudience
            .Where(permission => !targetPermissions.Contains(permission, StringComparer.OrdinalIgnoreCase))
            .ToList();

        var toAdd = targetPermissions
            .Where(permission => !existingForAudience.Contains(permission, StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (toRemove.Count > 0)
        {
            await DeletePermissionsAsync(auth0UserId, audience, toRemove, accessToken, cancellationToken);
        }

        if (toAdd.Count > 0)
        {
            await AddPermissionsAsync(auth0UserId, audience, toAdd, accessToken, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<Auth0PermissionRecord>> GetCurrentPermissionsAsync(
        string auth0UserId,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            BuildPath($"/api/v2/users/{Uri.EscapeDataString(auth0UserId)}/permissions?per_page=100&page=0"));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(response, "Failed to read current Auth0 user permissions.");
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var records = await JsonSerializer.DeserializeAsync<List<Auth0PermissionRecord>>(
            stream,
            JsonOptions,
            cancellationToken);

        return records ?? [];
    }

    private async Task DeletePermissionsAsync(
        string auth0UserId,
        string audience,
        IReadOnlyList<string> permissions,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Delete,
            BuildPath($"/api/v2/users/{Uri.EscapeDataString(auth0UserId)}/permissions"))
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new
                    {
                        permissions = permissions.Select(permission => new
                        {
                            resource_server_identifier = audience,
                            permission_name = permission
                        })
                    },
                    JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(response, "Failed to remove Auth0 user permissions.");
        }
    }

    private async Task AddPermissionsAsync(
        string auth0UserId,
        string audience,
        IReadOnlyList<string> permissions,
        string accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildPath($"/api/v2/users/{Uri.EscapeDataString(auth0UserId)}/permissions"))
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new
                    {
                        permissions = permissions.Select(permission => new
                        {
                            resource_server_identifier = audience,
                            permission_name = permission
                        })
                    },
                    JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(response, "Failed to assign Auth0 user permissions.");
        }
    }

    private async Task<string> GetManagementTokenAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, BuildPath("/oauth/token"))
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
            throw await CreateExceptionAsync(response, "Failed to request Auth0 management access token.");
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.TryGetProperty("access_token", out var tokenElement) &&
            tokenElement.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(tokenElement.GetString()))
        {
            return tokenElement.GetString()!;
        }

        throw new Auth0ManagementException("Auth0 returned an invalid management token response.", 502);
    }

    private bool IsConfiguredForSync()
    {
        return !string.IsNullOrWhiteSpace(_options.Domain)
            && !string.IsNullOrWhiteSpace(_options.ManagementClientId)
            && !string.IsNullOrWhiteSpace(_options.ManagementClientSecret)
            && !string.IsNullOrWhiteSpace(ResolvePermissionAudience());
    }

    private string ResolvePermissionAudience()
    {
        return string.IsNullOrWhiteSpace(_options.ManagementPermissionAudience)
            ? _options.Audience.Trim()
            : _options.ManagementPermissionAudience.Trim();
    }

    private static string BuildPath(string path)
    {
        return path.StartsWith('/') ? path : $"/{path}";
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
        }
        catch
        {
            // Keep fallback message.
        }

        return new Auth0ManagementException(message, (int)response.StatusCode);
    }

    private sealed class Auth0PermissionRecord
    {
        public string ResourceServerIdentifier { get; set; } = string.Empty;
        public string PermissionName { get; set; } = string.Empty;
    }
}
