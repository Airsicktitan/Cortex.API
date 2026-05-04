using Cortex.API.Services.Integrations;

namespace Cortex.API.Handlers;

internal static class SafeErrorResponses
{
    private const string GenericDetail =
        "A server-side error occurred. Use the trace id when contacting support.";

    public static IResult BadRequest(string title = "Request could not be completed")
    {
        return Results.Problem(
            title: title,
            detail: GenericDetail,
            statusCode: StatusCodes.Status400BadRequest);
    }

    public static IResult ServerError(string title = "Request could not be completed")
    {
        return Results.Problem(
            title: title,
            detail: GenericDetail,
            statusCode: StatusCodes.Status500InternalServerError);
    }

    public static IResult UpstreamError(string title = "Upstream service request failed")
    {
        return Results.Problem(
            title: title,
            detail: GenericDetail,
            statusCode: StatusCodes.Status502BadGateway);
    }

    /// <summary>Reports a dependency failure without forwarding the upstream HTTP status (always 502).</summary>
    public static IResult UpstreamError(int statusCode, string title)
    {
        // Do not forward arbitrary upstream status codes to API clients.
        return Results.Problem(
            title: title,
            detail: GenericDetail,
            statusCode: StatusCodes.Status502BadGateway);
    }

    public static IResult IntegrationApi(IntegrationApiException exception)
    {
        return Results.Problem(
            title: exception.Message,
            detail: GenericDetail,
            statusCode: exception.StatusCode);
    }
}
