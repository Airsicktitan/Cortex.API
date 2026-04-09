using Cortex.API.Models;

namespace Cortex.API.Services;

public interface IScheduledJobService
{
    Task<IReadOnlyList<ScheduledJob>> GetAllAsync();
    Task<ScheduledJob> CreateAsync(ScheduledJob job, int runAsUserId);
    Task<ScheduledJob> UpdateAsync(int id, ScheduledJob job, int runAsUserId);
    Task<ScheduledJob> RunNowAsync(int id);
    Task<int> RunDueJobsAsync(DateTime utcNow);
}
