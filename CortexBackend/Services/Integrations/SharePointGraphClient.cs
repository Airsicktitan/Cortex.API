using System.Net.Http.Headers;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Cortex.API.Configuration;
using Microsoft.Extensions.Options;

namespace Cortex.API.Services.Integrations;

public sealed record SharePointSiteRef(string Id);

public interface ISharePointGraphClient
{
    Task<SharePointSiteRef> GetSiteByPathAsync(
        string hostname,
        string siteRelativePath,
        string? tenantIdOverride,
        CancellationToken cancellationToken = default);

    Task<JsonElement> GetListAsync(
        string siteId,
        string listId,
        string? tenantIdOverride,
        CancellationToken cancellationToken = default);

    /// <summary>Validates application credentials by acquiring a Graph token (no resource-specific reads).</summary>
    Task ValidateGraphApplicationCredentialsAsync(
        string? tenantIdOverride,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JsonElement>> GetListColumnsAsync(
        string siteId,
        string listId,
        string? tenantIdOverride,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JsonElement>> GetListItemsAsync(
        string siteId,
        string listId,
        string? tenantIdOverride,
        CancellationToken cancellationToken = default);
}

public sealed class SharePointGraphClient : ISharePointGraphClient
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SharePointGraphOptions _options;

    public SharePointGraphClient(IHttpClientFactory httpClientFactory, IOptions<SharePointGraphOptions> options)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
    }

    public async Task<SharePointSiteRef> GetSiteByPathAsync(
        string hostname,
        string siteRelativePath,
        string? tenantIdOverride,
        CancellationToken cancellationToken = default)
    {
        // Graph: GET /sites/{hostname}:{/server-relative-path} — see Microsoft Graph site resource
        var path = siteRelativePath.StartsWith('/') ? siteRelativePath : "/" + siteRelativePath;
        var relativePath = $"sites/{hostname}:{path}";
        var response = await SendGraphAsync(
            HttpMethod.Get,
            relativePath,
            tenantIdOverride,
            cancellationToken).ConfigureAwait(false);

        using (var doc = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false))
        {
            if (!doc.RootElement.TryGetProperty("id", out var idProp))
            {
                throw new IntegrationApiException(502, "Microsoft Graph returned an unexpected site response.");
            }

            return new SharePointSiteRef(idProp.GetString() ?? throw new IntegrationApiException(502, "Microsoft Graph returned an empty site id."));
        }
    }

    public async Task<JsonElement> GetListAsync(
        string siteId,
        string listId,
        string? tenantIdOverride,
        CancellationToken cancellationToken = default)
    {
        var encodedSite = Uri.EscapeDataString(siteId);
        var encodedList = Uri.EscapeDataString(listId);
        var response = await SendGraphAsync(
            HttpMethod.Get,
            $"sites/{encodedSite}/lists/{encodedList}",
            tenantIdOverride,
            cancellationToken).ConfigureAwait(false);

        using (var doc = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false))
        {
            return doc.RootElement.Clone();
        }
    }

    public async Task ValidateGraphApplicationCredentialsAsync(
        string? tenantIdOverride,
        CancellationToken cancellationToken = default)
    {
        _ = await GetTokenAsync(tenantIdOverride, cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<JsonElement>> GetListColumnsAsync(
        string siteId,
        string listId,
        string? tenantIdOverride,
        CancellationToken cancellationToken = default)
    {
        var encodedSite = Uri.EscapeDataString(siteId);
        var encodedList = Uri.EscapeDataString(listId);
        return await CollectODataPagesAsync(
            $"sites/{encodedSite}/lists/{encodedList}/columns",
            tenantIdOverride,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<JsonElement>> GetListItemsAsync(
        string siteId,
        string listId,
        string? tenantIdOverride,
        CancellationToken cancellationToken = default)
    {
        var encodedSite = Uri.EscapeDataString(siteId);
        var encodedList = Uri.EscapeDataString(listId);
        var initial =
            $"sites/{encodedSite}/lists/{encodedList}/items?$expand=fields&$top=200";
        return await CollectODataPagesAsync(initial, tenantIdOverride, cancellationToken).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<JsonElement>> CollectODataPagesAsync(
        string initialRelativeUrl,
        string? tenantIdOverride,
        CancellationToken cancellationToken)
    {
        var results = new List<JsonElement>();
        string? next = initialRelativeUrl;

        while (next is not null)
        {
            var response = await SendGraphAsync(HttpMethod.Get, next, tenantIdOverride, cancellationToken, next.StartsWith("http", StringComparison.OrdinalIgnoreCase)).ConfigureAwait(false);
            using (var doc = await ReadJsonAsync(response, cancellationToken).ConfigureAwait(false))
            {
                var root = doc.RootElement;

                if (root.TryGetProperty("value", out var value) && value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in value.EnumerateArray())
                    {
                        results.Add(el.Clone());
                    }
                }

                next = null;
                if (root.TryGetProperty("@odata.nextLink", out var nextLink) && nextLink.ValueKind == JsonValueKind.String)
                {
                    next = nextLink.GetString();
                }
            }
        }

        return results;
    }

    private async Task<HttpResponseMessage> SendGraphAsync(
        HttpMethod method,
        string urlOrPath,
        string? tenantIdOverride,
        CancellationToken cancellationToken,
        bool urlIsAbsolute = false)
    {
        var token = await GetTokenAsync(tenantIdOverride, cancellationToken).ConfigureAwait(false);
        var client = _httpClientFactory.CreateClient("SharePointGraph");

        Uri requestUri;
        if (urlIsAbsolute)
        {
            requestUri = new Uri(urlOrPath, UriKind.Absolute);
        }
        else
        {
            var baseUrl = _options.GraphBaseUrl.TrimEnd('/');
            requestUri = new Uri($"{baseUrl}/{urlOrPath.TrimStart('/')}");
        }

        using var request = new HttpRequestMessage(method, requestUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new IntegrationApiException(502, "Microsoft Graph request timed out.");
        }
        catch (HttpRequestException)
        {
            throw new IntegrationApiException(502, "Unable to reach Microsoft Graph.");
        }

        return response;
    }

    private static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized ||
            response.StatusCode == System.Net.HttpStatusCode.Forbidden)
        {
            throw new IntegrationApiException(
                (int)response.StatusCode,
                "Microsoft Graph denied access. Verify app registration permissions and admin consent.");
        }

        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            throw new IntegrationApiException(404, "SharePoint site, list, or resource was not found in Microsoft Graph.");
        }

        if ((int)response.StatusCode >= 500)
        {
            throw new IntegrationApiException(502, "Microsoft Graph returned an error. Try again later.");
        }

        if (!response.IsSuccessStatusCode)
        {
            var safe = await TryExtractGraphErrorMessageAsync(response, cancellationToken).ConfigureAwait(false);
            if (response.StatusCode == System.Net.HttpStatusCode.BadRequest)
            {
                throw new IntegrationApiException(400, safe ?? "Microsoft Graph rejected the request.");
            }

            throw new IntegrationApiException(502, safe ?? "Microsoft Graph request failed.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken).ConfigureAwait(false);
        }
        catch (JsonException)
        {
            throw new IntegrationApiException(502, "Microsoft Graph returned invalid JSON.");
        }
    }

    private static async Task<string?> TryExtractGraphErrorMessageAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.TryGetProperty("error", out var err))
            {
                if (err.TryGetProperty("message", out var msg) && msg.ValueKind == JsonValueKind.String)
                {
                    return SanitizeGraphMessage(msg.GetString());
                }
            }
        }
        catch (JsonException)
        {
            // ignore
        }

        return null;
    }

    private static string? SanitizeGraphMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return null;
        }

        var trimmed = message.Trim();
        if (trimmed.Length > 280)
        {
            trimmed = trimmed[..280] + "…";
        }

        return trimmed;
    }

    private async Task<string> GetTokenAsync(string? tenantIdOverride, CancellationToken cancellationToken)
    {
        var (tenantId, clientId, clientSecret) = ResolveCredentials(tenantIdOverride);

        try
        {
            var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            var token = await credential.GetTokenAsync(
                new TokenRequestContext(["https://graph.microsoft.com/.default"]),
                cancellationToken).ConfigureAwait(false);
            return token.Token;
        }
        catch (CredentialUnavailableException)
        {
            throw new IntegrationApiException(502, "SharePoint Graph authentication failed. Verify credentials.");
        }
        catch (Azure.RequestFailedException)
        {
            throw new IntegrationApiException(502, "SharePoint Graph authentication failed. Verify credentials.");
        }
    }

    private (string TenantId, string ClientId, string ClientSecret) ResolveCredentials(string? tenantIdOverride)
    {
        var tenant = tenantIdOverride?.Trim();
        if (string.IsNullOrEmpty(tenant))
        {
            tenant = _options.TenantId?.Trim();
        }

        var clientId = _options.ClientId?.Trim();
        var secret = _options.ClientSecret?.Trim();

        if (string.IsNullOrEmpty(tenant) || string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(secret))
        {
            throw new IntegrationApiException(
                502,
                "SharePoint Graph is not configured. Set SharePointGraph:TenantId, ClientId, and ClientSecret (or connection TenantId with ClientId and ClientSecret).");
        }

        return (tenant, clientId, secret);
    }
}
