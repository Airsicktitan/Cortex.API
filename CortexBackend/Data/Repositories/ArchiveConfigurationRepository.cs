using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Data.Repositories;

public class ArchiveConfigurationRepository(CortexDbContext context) : IArchiveConfigurationRepository
{
    private readonly CortexDbContext _context = context;

    public async Task<ArchiveConfiguration?> GetAsync()
    {
        return await _context.ArchiveConfigurations
            .OrderBy(configuration => configuration.Id)
            .FirstOrDefaultAsync();
    }

    public async Task UpsertAsync(ArchiveConfiguration configuration)
    {
        var existingConfiguration = await GetAsync();
        if (existingConfiguration is null)
        {
            configuration.Id = 0;
            await _context.ArchiveConfigurations.AddAsync(configuration);
            return;
        }

        existingConfiguration.ArchiveAfterDays = configuration.ArchiveAfterDays;
        existingConfiguration.ArchiveResolvedTickets = configuration.ArchiveResolvedTickets;
        existingConfiguration.ArchiveClosedTickets = configuration.ArchiveClosedTickets;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
