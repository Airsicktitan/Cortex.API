using System.Net;
using System.Text;
using Cortex.API.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Cortex.API.Tests;

public class Auth0ManagementServicePatchTests
{
    [Fact]
    public async Task PatchUserRootProfileAsync_ClearNickname_SendsJsonNull_NotEmptyString()
    {
        string? patchBody = null;
        var handler = new StubHandler(async (req, ct) =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/oauth/token", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"access_token":"fake"}""",
                        Encoding.UTF8,
                        "application/json"),
                };
            }

            if (req.Method == HttpMethod.Patch)
            {
                patchBody = await req.Content!.ReadAsStringAsync(ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        });

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://tenant.auth0.com"),
        };

        var options = Options.Create(new Auth0ManagementOptions
        {
            Domain = "tenant.auth0.com",
            ManagementClientId = "id",
            ManagementClientSecret = "secret",
        });

        var sut = new Auth0ManagementService(client, options, NullLogger<Auth0ManagementService>.Instance);

        await sut.PatchUserRootProfileAsync("auth0|u", false, null, true, null, CancellationToken.None);

        Assert.NotNull(patchBody);
        Assert.Contains("\"nickname\":null", patchBody, StringComparison.Ordinal);
        Assert.DoesNotContain("\"nickname\":\"\"", patchBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PatchUserRootProfileAsync_UpdateNickname_SendsQuotedString()
    {
        string? patchBody = null;
        var handler = new StubHandler(async (req, ct) =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/oauth/token", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"access_token":"fake"}""",
                        Encoding.UTF8,
                        "application/json"),
                };
            }

            if (req.Method == HttpMethod.Patch)
            {
                patchBody = await req.Content!.ReadAsStringAsync(ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        });

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://t.auth0.com"),
        };
        var options = Options.Create(new Auth0ManagementOptions
        {
            Domain = "t.auth0.com",
            ManagementClientId = "id",
            ManagementClientSecret = "secret",
        });
        var sut = new Auth0ManagementService(client, options, NullLogger<Auth0ManagementService>.Instance);

        await sut.PatchUserRootProfileAsync("auth0|u", false, null, true, "adam", CancellationToken.None);

        Assert.NotNull(patchBody);
        Assert.Contains("\"nickname\":\"adam\"", patchBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task PatchUserRootProfileAsync_DisplayNameOnly_OmitsNicknameProperty()
    {
        string? patchBody = null;
        var handler = new StubHandler(async (req, ct) =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/oauth/token", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(
                        """{"access_token":"fake"}""",
                        Encoding.UTF8,
                        "application/json"),
                };
            }

            if (req.Method == HttpMethod.Patch)
            {
                patchBody = await req.Content!.ReadAsStringAsync(ct);
                return new HttpResponseMessage(HttpStatusCode.OK);
            }

            return new HttpResponseMessage(HttpStatusCode.BadRequest);
        });

        using var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://t.auth0.com"),
        };
        var options = Options.Create(new Auth0ManagementOptions
        {
            Domain = "t.auth0.com",
            ManagementClientId = "id",
            ManagementClientSecret = "secret",
        });
        var sut = new Auth0ManagementService(client, options, NullLogger<Auth0ManagementService>.Instance);

        await sut.PatchUserRootProfileAsync("auth0|u", true, "Adam Hooper", false, null, CancellationToken.None);

        Assert.NotNull(patchBody);
        Assert.Contains("\"name\":", patchBody, StringComparison.Ordinal);
        Assert.DoesNotContain("nickname", patchBody, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _send = send;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            _send(request, cancellationToken);
    }
}
