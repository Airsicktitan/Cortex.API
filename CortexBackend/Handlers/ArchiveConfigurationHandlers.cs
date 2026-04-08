using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;

namespace Cortex.API.Handlers;

public static class ArchiveConfigurationHandlers
{
    public static async Task<IResult> GetArchiveConfiguration(
        IArchiveConfigurationService archiveConfigurationService)
    {
        var configuration = await archiveConfigurationService.GetAsync();
        return Results.Ok(configuration.ToResponse());
    }

    public static async Task<IResult> UpdateArchiveConfiguration(
        UpdateArchiveConfigurationRequest request,
        IArchiveConfigurationService archiveConfigurationService)
    {
        try
        {
            var configuration = new ArchiveConfiguration
            {
                ArchiveAfterDays = request.ArchiveAfterDays,
                ArchiveResolvedTickets = request.ArchiveResolvedTickets,
                ArchiveClosedTickets = request.ArchiveClosedTickets
            };

            var savedConfiguration = await archiveConfigurationService.SaveAsync(configuration);
            return Results.Ok(savedConfiguration.ToResponse());
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    public static async Task<IResult> RunArchiveNow(
        ITicketArchivalService ticketArchivalService,
        IUserContextService userContextService)
    {
        var currentUser = await userContextService.GetCurrentUserAsync();
        var archivedTicketCount = await ticketArchivalService.ArchiveEligibleTicketsAsync(currentUser.Id);

        return Results.Ok(new { archivedTicketCount });
    }
}
