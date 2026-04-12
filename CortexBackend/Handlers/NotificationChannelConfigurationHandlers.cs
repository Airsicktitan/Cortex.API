using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;

namespace Cortex.API.Handlers;

public static class NotificationChannelConfigurationHandlers
{
    public static async Task<IResult> GetNotificationChannelConfiguration(
        INotificationChannelConfigurationService notificationChannelConfigurationService)
    {
        var configuration = await notificationChannelConfigurationService.GetAsync();
        return Results.Ok(configuration.ToResponse());
    }

    public static async Task<IResult> UpdateNotificationChannelConfiguration(
        UpdateNotificationChannelConfigurationRequest request,
        INotificationChannelConfigurationService notificationChannelConfigurationService)
    {
        try
        {
            var configuration = new NotificationChannelConfiguration
            {
                AssignmentChannel = ParseMode(
                    request.AssignmentChannel,
                    nameof(request.AssignmentChannel)),
                SlaRiskChannel = ParseMode(
                    request.SlaRiskChannel,
                    nameof(request.SlaRiskChannel))
            };

            var savedConfiguration = await notificationChannelConfigurationService.SaveAsync(
                configuration);
            return Results.Ok(savedConfiguration.ToResponse());
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    private static NotificationChannelMode ParseMode(string? rawValue, string fieldName)
    {
        if (Enum.TryParse<NotificationChannelMode>(rawValue, true, out var mode) &&
            Enum.IsDefined(mode))
        {
            return mode;
        }

        throw new ArgumentException(
            $"{fieldName} must be one of Neither, Email, Teams, or Both.",
            fieldName);
    }
}
