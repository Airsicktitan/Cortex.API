using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Data.Repositories;

public class ScheduledJobRepository(CortexDbContext context) : IScheduledJobRepository
{
    private readonly CortexDbContext _context = context;

    public async Task<List<ScheduledJob>> GetAllAsync()
    {
        return await _context.ScheduledJobs
            .Include(job => job.StoredProcedureDefinition)
            .Include(job => job.RunAsUser)
            .OrderBy(job => job.Name)
            .ToListAsync();
    }

    public Task<ScheduledJob?> GetByIdAsync(int id)
    {
        return _context.ScheduledJobs
            .Include(job => job.StoredProcedureDefinition)
            .Include(job => job.RunAsUser)
            .FirstOrDefaultAsync(job => job.Id == id);
    }

    public Task<ScheduledJob?> GetByNameAsync(string name)
    {
        return _context.ScheduledJobs.FirstOrDefaultAsync(job => job.Name == name);
    }

    public async Task<List<ScheduledJob>> GetDueJobsAsync(DateTime utcNow)
    {
        return await _context.ScheduledJobs
            .Include(job => job.StoredProcedureDefinition)
            .Include(job => job.RunAsUser)
            .Where(job =>
                job.IsEnabled &&
                job.NextRunDateUtc.HasValue &&
                job.NextRunDateUtc <= utcNow)
            .OrderBy(job => job.NextRunDateUtc)
            .ToListAsync();
    }

    public async Task AddAsync(ScheduledJob job)
    {
        await _context.ScheduledJobs.AddAsync(job);
    }

    public Task SaveChangesAsync()
    {
        return _context.SaveChangesAsync();
    }
}
