using Cortex.API.Services;

namespace Cortex.API.Handlers;

public static class IntakeLearningHandlers
{
    public static async Task<IResult> GetIntakeLearningOverview(
        IIntakeLearningService intakeLearningService,
        CancellationToken cancellationToken)
    {
        var overview = await intakeLearningService.GetOverviewAsync(cancellationToken);
        return Results.Ok(overview);
    }
}
