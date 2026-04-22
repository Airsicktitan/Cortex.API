using System.Text;
using System.Text.Json;
using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Models;
using Microsoft.Extensions.Logging;

namespace Cortex.API.Services;

public sealed class CortexAiAssessmentService(
    ITicketTriageAiService triageAiService,
    ITicketTriageVocabularyProvider vocabularyProvider,
    ITicketBoardService ticketBoardService,
    IUserRepository userRepository,
    ICommentRepository commentRepository,
    ICortexCandidateResolutionService candidateResolutionService,
    ILogger<CortexAiAssessmentService> logger) : ICortexAiAssessmentService
{
    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };

    public async Task<CortexAiAssessment> AssessTicketAsync(
        Ticket ticket,
        CancellationToken cancellationToken = default)
    {
        var vocabulary = await vocabularyProvider.GetAsync(cancellationToken);
        var board = await ticketBoardService.GetByIdAsync(ticket.BoardId);
        var boardName = board?.Name ?? $"Board #{ticket.BoardId}";

        User? requester = null;
        if (ticket.CreatedBy > 0)
        {
            requester = await userRepository.GetByIdAsync(ticket.CreatedBy);
        }

        var insight = TryParseScreenshotInsight(ticket.AiScreenshotInsightJson);
        var supplemental = await BuildSupplementalContextAsync(ticket, insight, cancellationToken);

        var candidates = (await candidateResolutionService.GetEligibleCandidatesAsync(ticket, cancellationToken))
            .Where(c => c.Eligible)
            .Select(c => (c.UserId, c.DisplayName))
            .ToList();

        var triageInput = new TicketTriageInput
        {
            Title = ticket.Title,
            Description = ticket.Description,
            CurrentPriority = ticket.Priority,
            Status = ticket.Status,
            Department = requester?.Department,
            BoardName = boardName,
            Vocabulary = vocabulary,
            SupplementalContext = string.IsNullOrWhiteSpace(supplemental) ? null : supplemental,
            EligibleOwnerCandidates = candidates.Count > 0 ? candidates : null,
        };

        TicketTriageGenerateResponse triage;
        try
        {
            triage = await triageAiService.GenerateTriageAsync(triageInput, cancellationToken);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Cortex AI assessment triage call failed for ticket {TicketId}", ticket.Id);
            return UnavailableAssessment(
                "AI assessment could not be completed due to an unexpected error.",
                ticket,
                vocabulary,
                insight);
        }

        if (triage.Unavailable)
        {
            return UnavailableAssessment(
                string.IsNullOrWhiteSpace(triage.UnavailableReason)
                    ? "AI assessment is unavailable."
                    : triage.UnavailableReason!,
                ticket,
                vocabulary,
                insight);
        }

        var confidence = 0.82m;
        var recommendedPriority = CortexAiAssessmentConstraintMapper.ResolvePriorityOrTicketDefault(
            triage.SuggestedPriority,
            ticket.Priority,
            vocabulary.Priorities,
            ref confidence);

        var recommendedStatus = triage.SuggestedStatus?.Trim() ?? string.Empty;
        if (vocabulary.Statuses.Count > 0
            && string.IsNullOrWhiteSpace(recommendedStatus)
            && !string.IsNullOrWhiteSpace(triage.SuggestedStatus))
        {
            confidence = Math.Max(0m, confidence - 0.06m);
        }

        var risk = CortexAiAssessmentConstraintMapper.NormalizeRisk(triage.PotentialSlaRisk, ref confidence);
        var recommendedCategory = triage.SuggestedCategory?.Trim() ?? string.Empty;

        var owner = triage.SuggestedOwnerUserId;
        if (candidates.Count == 0 && !string.IsNullOrWhiteSpace(owner))
        {
            owner = null;
            confidence = Math.Max(0m, confidence - 0.04m);
        }

        var reasons = new List<string>();
        if (!string.IsNullOrWhiteSpace(triage.PriorityReason))
        {
            reasons.Add(triage.PriorityReason);
        }

        if (!string.IsNullOrWhiteSpace(triage.SlaRiskReason))
        {
            reasons.Add(triage.SlaRiskReason);
        }

        if (reasons.Count == 0 && !string.IsNullOrWhiteSpace(triage.Summary))
        {
            reasons.Add("Assessment derived from unified intake and routing context.");
        }

        var evidence = BuildEvidence(insight);
        if (evidence.Count > 0)
        {
            confidence = Math.Min(1m, confidence + 0.03m);
        }

        confidence = Math.Clamp(Math.Round(confidence, 2, MidpointRounding.AwayFromZero), 0m, 1m);

        return new CortexAiAssessment
        {
            Summary = string.IsNullOrWhiteSpace(triage.Summary)
                ? "No summary returned from the assessment model."
                : triage.Summary.Trim(),
            RecommendedPriority = recommendedPriority,
            RecommendedStatus = recommendedStatus,
            RecommendedCategory = recommendedCategory,
            RecommendedOwnerUserId = owner,
            RiskLevel = risk,
            ConfidenceScore = confidence,
            Reasons = reasons.Take(5).ToList(),
            MissingInformation = triage.MissingDetails ?? [],
            Evidence = evidence,
        };
    }

    private async Task<string?> BuildSupplementalContextAsync(
        Ticket ticket,
        ScreenshotInsightPersistedDto? insight,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder(4096);
        if (ticket.Comments is { Count: > 0 })
        {
            sb.AppendLine("### Comments (in-memory snapshot, truncated)");
            foreach (var comment in ticket.Comments.OrderBy(c => c.CreatedDate).Take(40))
            {
                var body = (comment.Body ?? string.Empty).Trim();
                if (body.Length == 0)
                {
                    continue;
                }

                if (body.Length > 800)
                {
                    body = body[..800] + "…";
                }

                sb.AppendLine($"- {body}");
            }

            sb.AppendLine();
        }
        else if (!string.IsNullOrWhiteSpace(ticket.Id))
        {
            try
            {
                var comments = (await commentRepository.GetCommentsByTicketIdAsync(ticket.Id))
                    .OrderBy(c => c.CreatedDate)
                    .Take(40)
                    .ToList();
                if (comments.Count > 0)
                {
                    sb.AppendLine("### Comments (most recent last, truncated)");
                    foreach (var comment in comments)
                    {
                        var body = (comment.Body ?? string.Empty).Trim();
                        if (body.Length == 0)
                        {
                            continue;
                        }

                        if (body.Length > 800)
                        {
                            body = body[..800] + "…";
                        }

                        sb.AppendLine($"- {body}");
                    }

                    sb.AppendLine();
                }
            }
            catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Failed loading comments for AI assessment on ticket {TicketId}", ticket.Id);
            }
        }

        if (insight is not null && !string.IsNullOrWhiteSpace(insight.Summary))
        {
            sb.AppendLine("### Persisted screenshot / vision summary (advisory evidence)");
            sb.AppendLine(insight.Summary.Trim());
            if (insight.VisibleDetails.Count > 0)
            {
                sb.AppendLine("Visible details:");
                foreach (var line in insight.VisibleDetails.Take(6))
                {
                    sb.AppendLine($"- {line}");
                }
            }

            sb.AppendLine();
        }

        var text = sb.ToString().Trim();
        return text.Length == 0 ? null : text;
    }

    private static ScreenshotInsightPersistedDto? TryParseScreenshotInsight(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<ScreenshotInsightPersistedDto>(json, JsonSerializerOptions);
        }
        catch
        {
            return null;
        }
    }

    private static List<string> BuildEvidence(ScreenshotInsightPersistedDto? insight)
    {
        if (insight is null)
        {
            return [];
        }

        var lines = new List<string>();
        if (!string.IsNullOrWhiteSpace(insight.Summary))
        {
            lines.Add($"Vision summary: {insight.Summary.Trim()}");
        }

        lines.AddRange(
            insight.VisibleDetails
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .Take(4)
                .Select(s => $"Visible: {s}"));

        lines.AddRange(
            insight.PossibleIssues
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .Take(3)
                .Select(s => $"Possible issue: {s}"));

        return lines;
    }

    private static CortexAiAssessment UnavailableAssessment(
        string reason,
        Ticket ticket,
        TicketTriageVocabularySnapshot vocabulary,
        ScreenshotInsightPersistedDto? insight)
    {
        decimal confidence = 0.5m;
        var priority = CortexAiAssessmentConstraintMapper.ResolvePriorityOrTicketDefault(
            null,
            ticket.Priority,
            vocabulary.Priorities,
            ref confidence);
        confidence = 0.22m;
        var risk = CortexAiAssessmentConstraintMapper.NormalizeRisk("Low", ref confidence);

        return new CortexAiAssessment
        {
            Summary = reason,
            RecommendedPriority = priority,
            RecommendedStatus = ticket.Status?.Trim() ?? string.Empty,
            RecommendedCategory = string.Empty,
            RecommendedOwnerUserId = null,
            RiskLevel = risk,
            ConfidenceScore = Math.Clamp(confidence, 0m, 1m),
            Reasons = ["Deterministic defaults applied because AI output was unavailable or invalid."],
            MissingInformation = [],
            Evidence = BuildEvidence(insight),
        };
    }
}
