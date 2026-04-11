using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;

namespace Cortex.API.Handlers;

public static class ArchiveConfigurationHandlers
{
    public static async Task<IResult> GetArchiveConfigurations(
        IArchiveConfigurationService archiveConfigurationService,
        IArchiveAutomationService archiveAutomationService,
        IUserContextService userContextService)
    {
        var currentUser = await userContextService.GetCurrentUserAsync();
        await archiveAutomationService.EnsurePolicySchedulerAsync(currentUser.Id);
        var configurations = await archiveConfigurationService.GetAllAsync();
        return Results.Ok(configurations.Select(configuration => configuration.ToResponse()));
    }

    public static async Task<IResult> CreateArchiveConfiguration(
        UpdateArchiveConfigurationRequest request,
        IArchiveConfigurationService archiveConfigurationService,
        IArchiveAutomationService archiveAutomationService,
        IUserContextService userContextService)
    {
        try
        {
            var configuration = new ArchiveConfiguration
            {
                ArchiveAfterDays = request.ArchiveAfterDays,
                EligibleStatuses = [.. request.EligibleStatuses]
            };

            var savedConfiguration = await archiveConfigurationService.CreateAsync(configuration);
            var currentUser = await userContextService.GetCurrentUserAsync();
            await archiveAutomationService.EnsurePolicySchedulerAsync(currentUser.Id);
            return Results.Created($"/api/settings/archive/{savedConfiguration.Id}", savedConfiguration.ToResponse());
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    public static async Task<IResult> UpdateArchiveConfiguration(
        int id,
        UpdateArchiveConfigurationRequest request,
        IArchiveConfigurationService archiveConfigurationService,
        IArchiveAutomationService archiveAutomationService,
        IUserContextService userContextService)
    {
        try
        {
            var configuration = new ArchiveConfiguration
            {
                ArchiveAfterDays = request.ArchiveAfterDays,
                EligibleStatuses = [.. request.EligibleStatuses]
            };

            var savedConfiguration = await archiveConfigurationService.UpdateAsync(id, configuration);
            var currentUser = await userContextService.GetCurrentUserAsync();
            await archiveAutomationService.EnsurePolicySchedulerAsync(currentUser.Id);
            return Results.Ok(savedConfiguration.ToResponse());
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    public static async Task<IResult> DeleteArchiveConfiguration(
        int id,
        IArchiveConfigurationService archiveConfigurationService,
        IArchiveAutomationService archiveAutomationService,
        IUserContextService userContextService)
    {
        try
        {
            await archiveConfigurationService.DeleteAsync(id);
            var currentUser = await userContextService.GetCurrentUserAsync();
            await archiveAutomationService.EnsurePolicySchedulerAsync(currentUser.Id);
            return Results.NoContent();
        }
        catch (KeyNotFoundException)
        {
            return Results.NotFound();
        }
    }

    public static async Task<IResult> RunArchiveNow(
        ITicketArchivalService ticketArchivalService,
        IUserContextService userContextService,
        IArchiveAutomationService archiveAutomationService)
    {
        var currentUser = await userContextService.GetCurrentUserAsync();
        await archiveAutomationService.EnsurePolicySchedulerAsync(currentUser.Id);
        var archivedTicketCount = await ticketArchivalService.ArchiveEligibleTicketsAsync(currentUser.Id);

        return Results.Ok(new { archivedTicketCount });
    }
}
