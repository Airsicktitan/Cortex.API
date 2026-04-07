using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Data.Repositories;

public class SlaConfigurationRepository(CortexDbContext context) : ISlaConfigurationRepository
{
    private readonly CortexDbContext _context = context;

    public async Task<IReadOnlyList<SlaConfiguration>> GetAllAsync()
    {
        return await _context.SlaConfigurations
            .OrderBy(configuration => configuration.Priority)
            .ToListAsync();
    }

    public async Task UpsertRangeAsync(IEnumerable<SlaConfiguration> configurations)
    {
        var configurationList = configurations.ToList();
        var priorities = configurationList
            .Select(configuration => configuration.Priority)
            .ToList();

        var existingConfigurations = await _context.SlaConfigurations
            .Where(configuration => priorities.Contains(configuration.Priority))
            .ToDictionaryAsync(configuration => configuration.Priority, StringComparer.OrdinalIgnoreCase);

        foreach (var configuration in configurationList)
        {
            if (existingConfigurations.TryGetValue(configuration.Priority, out var existingConfiguration))
            {
                existingConfiguration.TargetHours = configuration.TargetHours;
                existingConfiguration.WarningHours = configuration.WarningHours;
                continue;
            }

            await _context.SlaConfigurations.AddAsync(configuration);
        }
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
