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

    public static IResult UpstreamError(int? statusCode, string title = "Upstream service request failed")
    {
        if (statusCode is >= 400 and < 500)
        {
            return Results.Problem(
                title: title,
                detail: GenericDetail,
                statusCode: statusCode);
        }

        return UpstreamError(title);
    }
}
