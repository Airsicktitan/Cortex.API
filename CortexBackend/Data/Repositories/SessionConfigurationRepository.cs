using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Data.Repositories;

public class SessionConfigurationRepository(CortexDbContext context) : ISessionConfigurationRepository
{
    private readonly CortexDbContext _context = context;

    public async Task<SessionConfiguration?> GetAsync()
    {
        return await _context.SessionConfigurations
            .OrderBy(configuration => configuration.Id)
            .FirstOrDefaultAsync();
    }

    public async Task UpsertAsync(SessionConfiguration configuration)
    {
        var existingConfiguration = await GetAsync();
        if (existingConfiguration is null)
        {
            configuration.Id = 0;
            await _context.SessionConfigurations.AddAsync(configuration);
            return;
        }

        existingConfiguration.InactivityTimeoutMinutes = configuration.InactivityTimeoutMinutes;
        existingConfiguration.WarningMinutes = configuration.WarningMinutes;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
