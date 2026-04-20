using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace Cortex.API.Tests;

internal static class ResultAssertions
{
    public static async Task AssertStatusCodeAsync(IResult result, int expectedStatusCode)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();
        // Results.Forbid() eventually dispatches through the authentication stack,
        // even in minimal-API handler tests. Register a no-op scheme so calls like
        // HttpContext.ForbidAsync() cleanly return a 403 instead of throwing.
        services
            .AddAuthentication(options => options.DefaultScheme = "TestScheme")
            .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                "TestScheme",
                _ => { });

        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
        };
        httpContext.Response.Body = new MemoryStream();

        await result.ExecuteAsync(httpContext);
        Assert.Equal(expectedStatusCode, httpContext.Response.StatusCode);
    }

    /// <summary>
    /// Minimal authentication handler used only so <c>Results.Forbid()</c> and friends
    /// have a scheme to dispatch through in unit tests. It writes a 403 status code and
    /// returns — no tokens, no cookies, no side-effects.
    /// </summary>
    private sealed class TestAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public TestAuthenticationHandler(
            Microsoft.Extensions.Options.IOptionsMonitor<AuthenticationSchemeOptions> options,
            Microsoft.Extensions.Logging.ILoggerFactory logger,
            System.Text.Encodings.Web.UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync() =>
            Task.FromResult(AuthenticateResult.NoResult());

        protected override Task HandleForbiddenAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        protected override Task HandleChallengeAsync(AuthenticationProperties properties)
        {
            Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }
    }
}
