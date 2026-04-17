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
        var httpContext = new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
        };
        httpContext.Response.Body = new MemoryStream();

        await result.ExecuteAsync(httpContext);
        Assert.Equal(expectedStatusCode, httpContext.Response.StatusCode);
    }
}
