using Cortex.API.Models;

namespace Cortex.API.Data.Repositories;

public interface IScheduledJobRepository
{
    Task<List<ScheduledJob>> GetAllAsync();
    Task<ScheduledJob?> GetByIdAsync(int id);
    Task<ScheduledJob?> GetByNameAsync(string name);
    Task<List<ScheduledJob>> GetDueJobsAsync(DateTime utcNow);
    Task AddAsync(ScheduledJob job);
    Task SaveChangesAsync();
}
