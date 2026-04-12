using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Data.Repositories;

public class NotificationChannelConfigurationRepository(CortexDbContext context)
    : INotificationChannelConfigurationRepository
{
    private readonly CortexDbContext _context = context;

    public async Task<NotificationChannelConfiguration?> GetAsync()
    {
        return await _context.NotificationChannelConfigurations
            .OrderBy(configuration => configuration.Id)
            .FirstOrDefaultAsync();
    }

    public async Task UpsertAsync(NotificationChannelConfiguration configuration)
    {
        var existingConfiguration = await GetAsync();
        if (existingConfiguration is null)
        {
            configuration.Id = 0;
            await _context.NotificationChannelConfigurations.AddAsync(configuration);
            return;
        }

        existingConfiguration.AssignmentChannel = configuration.AssignmentChannel;
        existingConfiguration.SlaRiskChannel = configuration.SlaRiskChannel;
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
