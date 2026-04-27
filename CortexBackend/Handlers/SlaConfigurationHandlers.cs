using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;

namespace Cortex.API.Handlers;

public static class SlaConfigurationHandlers
{
    public static async Task<IResult> GetSlaConfiguration(ISlaConfigurationService slaConfigurationService)
    {
        var configurations = await slaConfigurationService.GetAllAsync();
        return Results.Ok(configurations.Select(configuration => configuration.ToResponse()));
    }

    public static async Task<IResult> UpdateSlaConfiguration(
        UpdateSlaConfigurationRequest request,
        ISlaConfigurationService slaConfigurationService)
    {
        try
        {
            var configurations = request.Policies.Select(policy => new SlaConfiguration
            {
                Priority = policy.Priority,
                TargetHours = policy.TargetHours,
                WarningHours = policy.WarningHours
            });

            var savedConfigurations = await slaConfigurationService.SaveAsync(configurations);

            return Results.Ok(savedConfigurations.Select(configuration => configuration.ToResponse()));
        }
        catch (ArgumentException)
        {
            return SafeErrorResponses.BadRequest();
        }
    }
}
