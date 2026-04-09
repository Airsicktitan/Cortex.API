using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Data.Repositories;

public class ArchiveConfigurationRepository(CortexDbContext context) : IArchiveConfigurationRepository
{
    private readonly CortexDbContext _context = context;

    public async Task<IReadOnlyList<ArchiveConfiguration>> GetAllAsync()
    {
        return await _context.ArchiveConfigurations
            .OrderBy(configuration => configuration.ArchiveAfterDays)
            .ThenBy(configuration => configuration.Id)
            .ToListAsync();
    }

    public async Task<ArchiveConfiguration?> GetByIdAsync(int id)
    {
        return await _context.ArchiveConfigurations
            .OrderBy(configuration => configuration.Id)
            .FirstOrDefaultAsync(configuration => configuration.Id == id);
    }

    public async Task AddAsync(ArchiveConfiguration configuration)
    {
        await _context.ArchiveConfigurations.AddAsync(configuration);
    }

    public void Delete(ArchiveConfiguration configuration)
    {
        _context.ArchiveConfigurations.Remove(configuration);
    }

    public async Task SaveChangesAsync()
    {
        await _context.SaveChangesAsync();
    }
}
