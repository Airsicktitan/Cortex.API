using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Cortex.API.DTO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Cortex.API.Services;

public class Auth0ManagementService(
    HttpClient httpClient,
    IOptions<Auth0ManagementOptions> options,
    ILogger<Auth0ManagementService> logger) : IAuth0ManagementService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _httpClient = httpClient;
    private readonly Auth0ManagementOptions _options = options.Value;
    private readonly ILogger<Auth0ManagementService> _logger = logger;

    public async Task<string> CreateUserAsync(
        CreateUserRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureConfigured();

        var accessToken = await GetManagementTokenAsync(cancellationToken);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            BuildPath("/api/v2/users"))
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
            throw await CreateExceptionAsync(response, "Failed to create Auth0 user.", cancellationToken);
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
            BuildPath($"/api/v2/users/{Uri.EscapeDataString(auth0UserId)}"));

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            throw await CreateExceptionAsync(response, "Failed to delete Auth0 user.", cancellationToken);
        }
    }

    public async Task<IReadOnlyList<Auth0RoleDto>> GetAllRolesAsync(CancellationToken cancellationToken = default)
    {
        EnsureManagementApiConfigured();
        var accessToken = await GetManagementTokenAsync(cancellationToken);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Get,
            BuildPath("/api/v2/roles?per_page=100"));

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(response, "Failed to list Auth0 roles.", cancellationToken);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return DeserializeRoleArray(json);
    }

    public async Task<IReadOnlyList<Auth0RoleDto>> GetUserRolesAsync(
        string auth0UserId,
        CancellationToken cancellationToken = default)
    {
        EnsureManagementApiConfigured();
        if (string.IsNullOrWhiteSpace(auth0UserId))
        {
            return Array.Empty<Auth0RoleDto>();
        }

        var accessToken = await GetManagementTokenAsync(cancellationToken);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Get,
            BuildPath($"/api/v2/users/{Uri.EscapeDataString(auth0UserId)}/roles"));

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(response, "Failed to load Auth0 user roles.", cancellationToken);
        }

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return DeserializeRoleArray(json);
    }

    public async Task AssignRolesToUserAsync(
        string auth0UserId,
        IReadOnlyList<string> roleIds,
        CancellationToken cancellationToken = default)
    {
        EnsureManagementApiConfigured();
        if (roleIds.Count == 0)
        {
            return;
        }

        var accessToken = await GetManagementTokenAsync(cancellationToken);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Post,
            BuildPath($"/api/v2/users/{Uri.EscapeDataString(auth0UserId)}/roles"))
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { roles = roleIds.ToArray() }, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(response, "Failed to assign Auth0 roles.", cancellationToken);
        }
    }

    public async Task RemoveRolesFromUserAsync(
        string auth0UserId,
        IReadOnlyList<string> roleIds,
        CancellationToken cancellationToken = default)
    {
        EnsureManagementApiConfigured();
        if (roleIds.Count == 0)
        {
            return;
        }

        var accessToken = await GetManagementTokenAsync(cancellationToken);

        using var httpRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            BuildPath($"/api/v2/users/{Uri.EscapeDataString(auth0UserId)}/roles"))
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { roles = roleIds.ToArray() }, JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(response, "Failed to remove Auth0 roles.", cancellationToken);
        }
    }

    private static IReadOnlyList<Auth0RoleDto> DeserializeRoleArray(string json)
    {
        try
        {
            var parsed = JsonSerializer.Deserialize<List<Auth0RoleJson>>(json, JsonOptions);
            if (parsed is null || parsed.Count == 0)
            {
                return Array.Empty<Auth0RoleDto>();
            }

            return parsed
                .Where(r => !string.IsNullOrWhiteSpace(r.Id) && !string.IsNullOrWhiteSpace(r.Name))
                .Select(r => new Auth0RoleDto { Id = r.Id!, Name = r.Name! })
                .ToList();
        }
        catch (JsonException)
        {
            return Array.Empty<Auth0RoleDto>();
        }
    }

    private sealed class Auth0RoleJson
    {
        public string? Id { get; set; }
        public string? Name { get; set; }
    }

    private void EnsureManagementApiConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.ManagementClientSecret))
        {
            _logger.LogError(
                "Auth0 Management API: Auth0:ManagementClientSecret is missing or blank. " +
                "Set it in configuration, Azure App Settings, or user secrets (dotnet user-secrets). " +
                "Role listing and user role changes will fail until this is set.");
        }

        if (string.IsNullOrWhiteSpace(_options.Domain) ||
            string.IsNullOrWhiteSpace(_options.ManagementClientId) ||
            string.IsNullOrWhiteSpace(_options.ManagementClientSecret))
        {
            throw new InvalidOperationException(
                "Auth0 management API is not configured. Set Auth0:Domain, Auth0:ManagementClientId, and Auth0:ManagementClientSecret.");
        }
    }

    private string GetManagementApiAudience()
    {
        if (string.IsNullOrWhiteSpace(_options.Domain))
        {
            throw new InvalidOperationException("Auth0:Domain is required for Management API audience.");
        }

        return _options.ResolveManagementApiAudience();
    }

    private async Task<string> GetManagementTokenAsync(CancellationToken cancellationToken)
    {
        var audience = GetManagementApiAudience();
        _logger.LogDebug("Requesting Auth0 Management API token with audience {Audience}", audience);

        var request = new HttpRequestMessage(
            HttpMethod.Post,
            BuildPath("/oauth/token"))
        {
            Content = new StringContent(
                JsonSerializer.Serialize(
                    new
                    {
                        client_id = _options.ManagementClientId,
                        client_secret = _options.ManagementClientSecret,
                        audience,
                        grant_type = "client_credentials"
                    },
                    JsonOptions),
                Encoding.UTF8,
                "application/json")
        };

        using var response = await _httpClient.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw await CreateExceptionAsync(
                response,
                "Failed to request an Auth0 management access token.",
                cancellationToken);
        }

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (document.RootElement.TryGetProperty("access_token", out var tokenElement) &&
            tokenElement.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(tokenElement.GetString()))
        {
            return tokenElement.GetString()!;
        }

        throw new Auth0ManagementException(
            "Auth0 returned an invalid management token response.",
            502);
    }

    private static string BuildPath(string path)
    {
        return path.StartsWith('/') ? path : $"/{path}";
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

    private async Task<Auth0ManagementException> CreateExceptionAsync(
        HttpResponseMessage response,
        string fallbackMessage,
        CancellationToken cancellationToken = default)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var bodyPreview = body.Length > 2048 ? body[..2048] + "…" : body;
        _logger.LogWarning(
            "Auth0 Management API call failed: {StatusCode} {ReasonPhrase}. ResponseLength={ResponseLength}",
            (int)response.StatusCode,
            response.ReasonPhrase,
            bodyPreview.Length);

        var message = fallbackMessage;
        try
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                message = $"{fallbackMessage} (HTTP {(int)response.StatusCode})";
                return new Auth0ManagementException(message, (int)response.StatusCode);
            }

            using var document = JsonDocument.Parse(body);

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
        catch (JsonException)
        {
            message = string.IsNullOrWhiteSpace(body)
                ? $"{fallbackMessage} (HTTP {(int)response.StatusCode})"
                : $"{fallbackMessage}: {bodyPreview}";
        }

        return new Auth0ManagementException(message, (int)response.StatusCode);
    }

    public async Task<IReadOnlyList<Auth0DirectoryUserDto>> GetAllDirectoryUsersAsync(
        CancellationToken cancellationToken = default)
    {
        EnsureManagementApiConfigured();
        var accessToken = await GetManagementTokenAsync(cancellationToken);
        var all = new List<Auth0DirectoryUserDto>();
        var page = 0;
        const int perPage = 100;

        while (true)
        {
            var path = $"/api/v2/users?per_page={perPage}&page={page}";
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, BuildPath(path));
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw await CreateExceptionAsync(response, "Failed to list Auth0 users.", cancellationToken);
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            List<Auth0DirectoryUserDto>? batch;
            try
            {
                batch = JsonSerializer.Deserialize<List<Auth0DirectoryUserDto>>(json, JsonOptions);
            }
            catch (JsonException)
            {
                throw new Auth0ManagementException("Auth0 returned an invalid user list response.", 502);
            }

            if (batch is null || batch.Count == 0)
            {
                break;
            }

            all.AddRange(batch);
            if (batch.Count < perPage)
            {
                break;
            }

            page++;
        }

        return all;
    }
}
