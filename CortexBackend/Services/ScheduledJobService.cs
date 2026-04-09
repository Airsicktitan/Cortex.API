using Cortex.API.Data.Repositories;
using Cortex.API.Database;
using Cortex.API.Models;
using Microsoft.EntityFrameworkCore;

namespace Cortex.API.Services;

public class ScheduledJobService(
    IScheduledJobRepository repository,
    IStoredProcedureDefinitionRepository storedProcedureRepository,
    ITicketArchivalService ticketArchivalService,
    CortexDbContext dbContext) : IScheduledJobService
{
    private readonly IScheduledJobRepository _repository = repository;
    private readonly IStoredProcedureDefinitionRepository _storedProcedureRepository = storedProcedureRepository;
    private readonly ITicketArchivalService _ticketArchivalService = ticketArchivalService;
    private readonly CortexDbContext _dbContext = dbContext;

    public async Task<IReadOnlyList<ScheduledJob>> GetAllAsync()
    {
        return await _repository.GetAllAsync();
    }

    public async Task<ScheduledJob> CreateAsync(ScheduledJob job, int runAsUserId)
    {
        var normalized = await NormalizeAsync(job, runAsUserId);
        await ValidateAsync(normalized, null);

        await _repository.AddAsync(normalized);
        await _repository.SaveChangesAsync();

        return (await _repository.GetByIdAsync(normalized.Id)) ?? normalized;
    }

    public async Task<ScheduledJob> UpdateAsync(int id, ScheduledJob job, int runAsUserId)
    {
        var existing = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Scheduled job was not found.");

        var normalized = await NormalizeAsync(job, runAsUserId);
        await ValidateAsync(normalized, id);

        existing.Name = normalized.Name;
        existing.Description = normalized.Description;
        existing.JobType = normalized.JobType;
        existing.IntervalMinutes = normalized.IntervalMinutes;
        existing.IsEnabled = normalized.IsEnabled;
        existing.StoredProcedureDefinitionId = normalized.StoredProcedureDefinitionId;
        existing.RunAsUserId = normalized.RunAsUserId;
        existing.LastModifiedDateUtc = DateTime.UtcNow;
        existing.NextRunDateUtc = normalized.NextRunDateUtc;

        await _repository.SaveChangesAsync();
        return (await _repository.GetByIdAsync(existing.Id)) ?? existing;
    }

    public async Task<ScheduledJob> RunNowAsync(int id)
    {
        var job = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException("Scheduled job was not found.");

        await ExecuteJobAsync(job, DateTime.UtcNow);
        await _repository.SaveChangesAsync();

        return (await _repository.GetByIdAsync(job.Id)) ?? job;
    }

    public async Task<int> RunDueJobsAsync(DateTime utcNow)
    {
        var dueJobs = await _repository.GetDueJobsAsync(utcNow);
        var executedCount = 0;

        foreach (var job in dueJobs)
        {
            await ExecuteJobAsync(job, utcNow);
            executedCount += 1;
        }

        if (executedCount > 0)
        {
            await _repository.SaveChangesAsync();
        }

        return executedCount;
    }

    private async Task ExecuteJobAsync(ScheduledJob job, DateTime utcNow)
    {
        try
        {
            var message = job.JobType switch
            {
                ScheduledJobType.ArchiveEligibleTickets => await RunArchiveJobAsync(job),
                ScheduledJobType.RunStoredProcedure => await RunStoredProcedureJobAsync(job),
                _ => throw new InvalidOperationException("Unsupported job type.")
            };

            job.LastRunStatus = "Succeeded";
            job.LastRunMessage = message;
        }
        catch (Exception exception)
        {
            job.LastRunStatus = "Failed";
            job.LastRunMessage = exception.Message;
        }

        job.LastRunDateUtc = utcNow;
        job.NextRunDateUtc = job.IsEnabled
            ? utcNow.AddMinutes(job.IntervalMinutes)
            : null;
    }

    private async Task<string> RunArchiveJobAsync(ScheduledJob job)
    {
        var archivedCount = await _ticketArchivalService.ArchiveEligibleTicketsAsync(job.RunAsUserId);
        return archivedCount == 1
            ? "Archived 1 eligible ticket."
            : $"Archived {archivedCount} eligible tickets.";
    }

    private async Task<string> RunStoredProcedureJobAsync(ScheduledJob job)
    {
        if (job.StoredProcedureDefinition is null)
        {
            throw new InvalidOperationException("Stored procedure definition is missing.");
        }

        if (!job.StoredProcedureDefinition.IsEnabled)
        {
            throw new InvalidOperationException("Stored procedure definition is disabled.");
        }

        var qualifiedName = BuildQualifiedProcedureName(job.StoredProcedureDefinition.ProcedureName);
        // Procedure names are validated on save and re-qualified here to avoid arbitrary SQL text.
#pragma warning disable EF1002
        await _dbContext.Database.ExecuteSqlRawAsync($"EXEC {qualifiedName}");
#pragma warning restore EF1002
        return $"Executed {job.StoredProcedureDefinition.ProcedureName}.";
    }

    private async Task<ScheduledJob> NormalizeAsync(ScheduledJob job, int runAsUserId)
    {
        StoredProcedureDefinition? storedProcedure = null;

        if (job.StoredProcedureDefinitionId.HasValue)
        {
            storedProcedure = await _storedProcedureRepository.GetByIdAsync(job.StoredProcedureDefinitionId.Value)
                ?? throw new ArgumentException("Selected stored procedure was not found.");
        }

        return new ScheduledJob
        {
            Name = job.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(job.Description) ? null : job.Description.Trim(),
            JobType = job.JobType,
            IntervalMinutes = job.IntervalMinutes,
            IsEnabled = job.IsEnabled,
            StoredProcedureDefinitionId = job.StoredProcedureDefinitionId,
            RunAsUserId = runAsUserId,
            CreatedDateUtc = job.CreatedDateUtc == default ? DateTime.UtcNow : job.CreatedDateUtc,
            NextRunDateUtc = job.IsEnabled ? DateTime.UtcNow.AddMinutes(job.IntervalMinutes) : null,
            StoredProcedureDefinition = storedProcedure
        };
    }

    private async Task ValidateAsync(ScheduledJob job, int? existingId)
    {
        if (string.IsNullOrWhiteSpace(job.Name))
        {
            throw new ArgumentException("Job name is required.");
        }

        if (job.IntervalMinutes <= 0)
        {
            throw new ArgumentException("Interval minutes must be greater than zero.");
        }

        var duplicateName = await _repository.GetByNameAsync(job.Name);
        if (duplicateName is not null && duplicateName.Id != existingId)
        {
            throw new ArgumentException("A scheduled job with this name already exists.");
        }

        if (job.JobType == ScheduledJobType.RunStoredProcedure)
        {
            if (!job.StoredProcedureDefinitionId.HasValue)
            {
                throw new ArgumentException("Stored procedure jobs require a stored procedure.");
            }

            var definition = job.StoredProcedureDefinition
                ?? await _storedProcedureRepository.GetByIdAsync(job.StoredProcedureDefinitionId.Value);

            if (definition is null)
            {
                throw new ArgumentException("Selected stored procedure was not found.");
            }
        }
        else
        {
            job.StoredProcedureDefinitionId = null;
        }
    }

    private static string BuildQualifiedProcedureName(string procedureName)
    {
        var parts = procedureName.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length is < 1 or > 2)
        {
            throw new ArgumentException("Stored procedure names must be schema-qualified or procedure-only.");
        }

        return string.Join(".", parts.Select(part => $"[{part}]"));
    }
}
