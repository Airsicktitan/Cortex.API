using Cortex.API.Data.Repositories;
using Cortex.API.Models;

namespace Cortex.API.Services;

public class ArchiveAutomationService(
    IArchiveConfigurationRepository archiveConfigurationRepository,
    IScheduledJobRepository scheduledJobRepository) : IArchiveAutomationService
{
    public const string ManagedArchiveJobName = "Archive Policy Automation";
    private const int DefaultIntervalMinutes = 60;

    private readonly IArchiveConfigurationRepository _archiveConfigurationRepository = archiveConfigurationRepository;
    private readonly IScheduledJobRepository _scheduledJobRepository = scheduledJobRepository;

    public async Task EnsurePolicySchedulerAsync(int runAsUserId)
    {
        var archivePolicies = await _archiveConfigurationRepository.GetAllAsync();
        var jobs = await _scheduledJobRepository.GetAllAsync();
        var archiveJobs = jobs
            .Where(job => job.JobType == ScheduledJobType.ArchiveEligibleTickets)
            .ToList();

        var managedJob = archiveJobs.FirstOrDefault(job =>
            string.Equals(job.Name, ManagedArchiveJobName, StringComparison.Ordinal));

        if (archivePolicies.Count == 0)
        {
            if (managedJob is null || !managedJob.IsEnabled)
            {
                return;
            }

            managedJob.IsEnabled = false;
            managedJob.NextRunDateUtc = null;
            managedJob.LastModifiedDateUtc = DateTime.UtcNow;
            managedJob.RunAsUserId = runAsUserId;
            await _scheduledJobRepository.SaveChangesAsync();
            return;
        }

        if (archiveJobs.Any(job => job.IsEnabled))
        {
            if (managedJob is not null && managedJob.RunAsUserId != runAsUserId)
            {
                managedJob.RunAsUserId = runAsUserId;
                managedJob.LastModifiedDateUtc = DateTime.UtcNow;
                await _scheduledJobRepository.SaveChangesAsync();
            }

            return;
        }

        if (managedJob is not null)
        {
            managedJob.IsEnabled = true;
            managedJob.RunAsUserId = runAsUserId;
            managedJob.LastModifiedDateUtc = DateTime.UtcNow;
            managedJob.NextRunDateUtc ??= DateTime.UtcNow.AddMinutes(managedJob.IntervalMinutes);
            await _scheduledJobRepository.SaveChangesAsync();
            return;
        }

        var job = new ScheduledJob
        {
            Name = ManagedArchiveJobName,
            Description = "System-managed automatic archive job created when archive policies exist.",
            JobType = ScheduledJobType.ArchiveEligibleTickets,
            IntervalMinutes = DefaultIntervalMinutes,
            IsEnabled = true,
            RunAsUserId = runAsUserId,
            CreatedDateUtc = DateTime.UtcNow,
            NextRunDateUtc = DateTime.UtcNow.AddMinutes(DefaultIntervalMinutes)
        };

        await _scheduledJobRepository.AddAsync(job);
        await _scheduledJobRepository.SaveChangesAsync();
    }
}
