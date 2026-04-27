using System.Text;
using Cortex.API.Handlers;
using Cortex.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace Cortex.API.Tests;

public sealed class UserHandlersSecurityResponseTests
{
    [Fact]
    public async Task GetAvailableAuth0Roles_DoesNotExposeUpstreamExceptionDetails()
    {
        var auth0 = new Mock<IAuth0ManagementService>(MockBehavior.Strict);
        auth0.Setup(service => service.GetAllRolesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Auth0ManagementException("upstream-token-leak", 500));

        var result = await UserHandlers.GetAvailableAuth0Roles(auth0.Object, CancellationToken.None);
        var body = await ExecuteAndReadBodyAsync(result);

        Assert.Equal(StatusCodes.Status502BadGateway, body.StatusCode);
        Assert.DoesNotContain("upstream-token-leak", body.Payload, StringComparison.Ordinal);
        Assert.Contains("server-side error occurred", body.Payload, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<(int StatusCode, string Payload)> ExecuteAndReadBodyAsync(IResult result)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddProblemDetails();

        var context = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider()
        };
        context.Response.Body = new MemoryStream();

        await result.ExecuteAsync(context);
        context.Response.Body.Position = 0;
        using var reader = new StreamReader(context.Response.Body, Encoding.UTF8, leaveOpen: true);
        var payload = await reader.ReadToEndAsync();
        return (context.Response.StatusCode, payload);
    }
}
