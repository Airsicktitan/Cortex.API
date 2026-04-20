using System.Text.Json;
using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cortex.API.Services;

/// <summary>Generates advisory triage and persists it on the ticket when the model returns usable content.</summary>
public static class TicketTriagePersistence
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    public static async Task TryGenerateAndPersistAsync(
        Ticket ticket,
        ITicketRepository repo,
        ITicketTriageAiService triageAi,
        ITicketTriageVocabularyProvider triageVocabulary,
        IUserRepository userRepository,
        ITicketBoardService ticketBoardService,
        AiSettingsConfiguration aiSettings,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        if (ticket.ApprovalStatus != ApprovalStatus.PendingApproval)
        {
            return;
        }

        var log = logger ?? NullLogger.Instance;
        var vocabulary = await triageVocabulary.GetAsync(cancellationToken);

        var board = await ticketBoardService.GetByIdAsync(ticket.BoardId);
        var boardName = board?.Name ?? $"Board #{ticket.BoardId}";
        var requester = await userRepository.GetByIdAsync(ticket.CreatedBy);

        var input = new TicketTriageInput
        {
            Title = ticket.Title,
            Description = ticket.Description,
            CurrentPriority = ticket.Priority,
            Status = ticket.Status,
            Department = requester?.Department,
            BoardName = boardName,
            Vocabulary = vocabulary,
        };

        var result = await triageAi.GenerateTriageAsync(input, cancellationToken);
        if (result.Unavailable)
        {
            return;
        }

        ApplyPersistedResult(ticket, result, vocabulary, aiSettings, log);
        await repo.UpdateTicketAsync(ticket);
        await repo.SaveChangesAsync();
    }

    public static void ApplyPersistedResult(
        Ticket ticket,
        TicketTriageGenerateResponse result,
        TicketTriageVocabularySnapshot vocabulary,
        AiSettingsConfiguration aiSettings,
        ILogger? logger = null)
    {
        var log = logger ?? NullLogger.Instance;

        ticket.AiTriageSummary = result.Summary;

        var priorityNames = vocabulary.Priorities.Select(priority => priority.Name).ToList();
        var validatedPriority = MatchAllowed(
            result.SuggestedPriority,
            priorityNames,
            "suggestedPriority",
            log);
        ticket.AiTriageSuggestedPriority = validatedPriority;
        ticket.AiTriagePriorityReason = result.PriorityReason;

        var statusNames = vocabulary.Statuses.Select(status => status.Name).ToList();
        var validatedStatus = vocabulary.Statuses.Count > 0
            ? MatchAllowed(result.SuggestedStatus, statusNames, "suggestedStatus", log)
            : null;
        ticket.AiTriageSuggestedStatus = validatedStatus;

        ticket.AiTriageMissingDetailsJson = JsonSerializer.Serialize(
            result.MissingDetails ?? [],
            JsonOptions);
        ticket.AiTriagePotentialSlaRisk = result.PotentialSlaRisk;
        ticket.AiTriageSlaRiskReason = result.SlaRiskReason;

        if (aiSettings.AdvisoryOnlyMode || aiSettings.SuggestionOnlyMode)
        {
            return;
        }

        ApplyAiSuggestedPriorityToTicket(ticket, validatedPriority);
        ApplyAiSuggestedStatusToTicket(ticket, validatedStatus);
    }

    /// <summary>
    /// Updates <see cref="Ticket.Priority"/> to match AI triage when the suggestion maps to a configured SLA priority.
    /// </summary>
    public static void ApplyAiSuggestedPriorityToTicket(Ticket ticket, string? validatedSuggestedPriority)
    {
        if (string.IsNullOrWhiteSpace(validatedSuggestedPriority))
        {
            return;
        }

        ticket.Priority = validatedSuggestedPriority.Trim();
    }

    /// <summary>
    /// Updates <see cref="Ticket.Status"/> when triage proposes a valid enabled status.
    /// </summary>
    public static void ApplyAiSuggestedStatusToTicket(Ticket ticket, string? validatedSuggestedStatus)
    {
        if (string.IsNullOrWhiteSpace(validatedSuggestedStatus))
        {
            return;
        }

        ticket.Status = validatedSuggestedStatus.Trim();
    }

    private static string? MatchAllowed(
        string? raw,
        IReadOnlyList<string> allowed,
        string fieldLabel,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var trimmed = raw.Trim();
        foreach (var name in allowed)
        {
            if (string.Equals(trimmed, name, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        logger.LogWarning(
            "AI triage field {Field} rejected: value not in configured vocabulary: {Value}",
            fieldLabel,
            trimmed);

        return null;
    }
}
