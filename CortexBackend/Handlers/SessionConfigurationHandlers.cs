using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;

namespace Cortex.API.Handlers;

public static class SessionConfigurationHandlers
{
    public static async Task<IResult> GetSessionConfiguration(
        ISessionConfigurationService sessionConfigurationService)
    {
        var configuration = await sessionConfigurationService.GetAsync();
        return Results.Ok(configuration.ToResponse());
    }

    public static async Task<IResult> UpdateSessionConfiguration(
        UpdateSessionConfigurationRequest request,
        ISessionConfigurationService sessionConfigurationService)
    {
        try
        {
            var configuration = new SessionConfiguration
            {
                InactivityTimeoutMinutes = request.InactivityTimeoutMinutes,
                WarningMinutes = request.WarningMinutes
            };

            var savedConfiguration = await sessionConfigurationService.SaveAsync(configuration);
            return Results.Ok(savedConfiguration.ToResponse());
        }
        catch (ArgumentException)
        {
            return SafeErrorResponses.BadRequest();
        }
    }
}
