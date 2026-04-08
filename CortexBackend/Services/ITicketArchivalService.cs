namespace Cortex.API.Services;

public interface ITicketArchivalService
{
    Task<int> ArchiveEligibleTicketsAsync(int archivedBy);
}
