using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Cortex.API.Models;
using Microsoft.Extensions.Options;

namespace Cortex.API.Services;

public class Auth0UserRoleSyncService(
    HttpClient httpClient,
    IOptions<Auth0ManagementOptions> options,
    ILogger<Auth0UserRoleSyncService> logger) : IAuth0UserRoleSyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly HttpClient _httpClient = httpClient;
    private readonly Auth0ManagementOptions _options = options.Value;
    private readonly ILogger<Auth0UserRoleSyncService> _logger = logger;

    public async Task SyncRoleToAuth0Async(User user, CancellationToken cancellationToken = default)
    {
        if (!_options.EnableUserAccessSync)
        {
            _logger.LogDebug("Auth0 role sync disabled; skipping user {UserId}.", user.Id);
            return;
        }

        if (string.IsNullOrWhiteSpace(user.Auth0Id))
        {
            _logger.LogDebug("No Auth0Id; skipping role sync for user {UserId}.", user.Id);
            return;
        }

        if (!IsConfigured())
        {
            _logger.LogWarning("Auth0 management not fully configured; skipping role sync for user {UserId}.", user.Id);
            return;
        }

        try
        {
            var accessToken = await GetManagementTokenAsync(cancellationToken);
            var role = string.IsNullOrWhiteSpace(user.Role) ? Auth0Roles.User : user.Role.Trim();

            using var request = new HttpRequestMessage(
                new HttpMethod("PATCH"),
                BuildPath($"/api/v2/users/{Uri.EscapeDataString(user.Auth0Id!)}"))
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(
                        new
                        {
                            app_metadata = new { role },
                            user_metadata = new { cortex_role = role }
                        },
                        JsonOptions),
                    Encoding.UTF8,
                    "application/json")
            };

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                var detail = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning(
                    "Auth0 role sync failed for user {UserId}: {Status} {Detail}",
                    user.Id,
                    (int)response.StatusCode,
                    detail);
                return;
            }

            _logger.LogInformation("Auth0 role sync completed for user {UserId}.", user.Id);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Auth0 role sync failed for user {UserId}.", user.Id);
        }
    }

    private bool IsConfigured()
    {
        return !string.IsNullOrWhiteSpace(_options.Domain)
            && !string.IsNullOrWhiteSpace(_options.ManagementClientId)
            && !string.IsNullOrWhiteSpace(_options.ManagementClientSecret);
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
                        audience = _options.ResolveManagementApiAudience(),
                        grant_type = "client_credentials"
                    },
                    JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.TryGetProperty("access_token", out var tokenElement) &&
            tokenElement.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(tokenElement.GetString()))
        {
            return tokenElement.GetString()!;
        }

        throw new InvalidOperationException("Auth0 returned an invalid management token response.");
    }

    private static string BuildPath(string path)
    {
        return path.StartsWith('/') ? path : $"/{path}";
    }
}
