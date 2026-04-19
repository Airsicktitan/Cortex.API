using Cortex.API.Data.Repositories;
using Cortex.API.Models;

namespace Cortex.API.Services;

/// <summary>One enabled ticket status from Cortex configuration (controlled vocabulary for AI).</summary>
public sealed record TicketTriageStatusOption(
    string Name,
    string? Description,
    int SortKey);

/// <summary>One priority tier from SLA configuration (controlled vocabulary for AI).</summary>
public sealed record TicketTriagePriorityOption(
    string Name,
    int TargetHours,
    int WarningHours);

/// <summary>Snapshot of Cortex-configured statuses and priorities passed to the triage model.</summary>
public sealed class TicketTriageVocabularySnapshot
{
    public IReadOnlyList<TicketTriageStatusOption> Statuses { get; init; } = [];
    public IReadOnlyList<TicketTriagePriorityOption> Priorities { get; init; } = [];

    public bool IsEmpty => Statuses.Count == 0 && Priorities.Count == 0;
}

/// <summary>Loads ticket status definitions and SLA priority policies as the AI vocabulary source of truth.</summary>
public interface ITicketTriageVocabularyProvider
{
    Task<TicketTriageVocabularySnapshot> GetAsync(CancellationToken cancellationToken = default);
}

public sealed class TicketTriageVocabularyProvider(
    ITicketStatusDefinitionRepository statusRepository,
    ISlaConfigurationService slaConfigurationService) : ITicketTriageVocabularyProvider
{
    private readonly ITicketStatusDefinitionRepository _statusRepository = statusRepository;
    private readonly ISlaConfigurationService _slaConfigurationService = slaConfigurationService;

    public async Task<TicketTriageVocabularySnapshot> GetAsync(CancellationToken cancellationToken = default)
    {
        var definitions = await _statusRepository.GetAllAsync();
        var statuses = definitions
            .Where(d => d.IsEnabled && !string.IsNullOrWhiteSpace(d.Name.Trim()))
            .OrderBy(d => d.Id)
            .Select(d => new TicketTriageStatusOption(
                d.Name.Trim(),
                string.IsNullOrWhiteSpace(d.Description) ? null : d.Description.Trim(),
                d.Id))
            .ToList();

        var slaRows = await _slaConfigurationService.GetAllAsync();
        var priorities = slaRows
            .Where(p => !string.IsNullOrWhiteSpace(p.Priority.Trim()))
            .GroupBy(p => p.Priority.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .Select(p => new TicketTriagePriorityOption(
                p.Priority.Trim(),
                p.TargetHours,
                p.WarningHours))
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new TicketTriageVocabularySnapshot
        {
            Statuses = statuses,
            Priorities = priorities,
        };
    }
}
