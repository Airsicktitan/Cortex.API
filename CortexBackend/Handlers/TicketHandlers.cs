namespace Cortex.API.Handlers;

using Cortex.API.Models;
using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Services;
using Cortex.API.Validation;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Text.Json;

/// <summary>
/// Defines all ticket-related API handlers for CORTEX.
/// Implements RESTful CRUD operations with database persistence via repository pattern.
///
/// Known Limitations:
/// - POST endpoint still uses sequential ticket ID generation
/// - Sequential ID generation is vulnerable to concurrency collisions under heavy parallel creates
/// </summary>
public static class TicketHandlers
{
    private static readonly string[] AllowedPriorities = ["Critical", "High", "Medium", "Low"];
    private const int MaxTitleLength = 200;
    private const int MaxDescriptionLength = 4000;
    private const int MaxApprovalReasonLength = 2000;

    public static async Task<IResult> GetAllTickets(
        DateTimeOffset? sinceUtc,
        int? boardId,
        int? page,
        int? pageSize,
        string? sort,
        bool? unpaged,
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        ISlaConfigurationService slaConfigurationService,
        IResponseMappingContextFactory mappingContextFactory,
        [FromServices] IOperationalRiskService operationalRiskService,
        [FromServices] IReassignmentRecommendationService reassignmentRecommendationService,
        [FromServices] IDecisionImpactService decisionImpactService)
    {
        if (!QueryParameterValidation.TryValidateOptionalBoardId(boardId, out var normalizedBoardId, out var boardIdError))
        {
            return Results.BadRequest(new { message = boardIdError });
        }

        if (!QueryParameterValidation.TryNormalizeTicketListSort(sort, out var normalizedSort, out var sortError))
        {
            return Results.BadRequest(new { message = sortError });
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();

        if (sinceUtc.HasValue)
        {
            var tickets = await repo.GetAllTicketsAsync(
                sinceUtc.Value.UtcDateTime,
                normalizedBoardId,
                visibilityContext);

            var mappingContext = await mappingContextFactory.CreateAsync(
                tickets.Select(ticket => ticket.CreatedBy),
                null,
                tickets.Select(ticket => ticket.BoardId));

            var items = await MapTicketResponsesAsync(
                tickets,
                slaConfigurations,
                mappingContext,
                operationalRiskService,
                reassignmentRecommendationService,
                decisionImpactService);

            return Results.Ok(new PagedTicketListResponse
            {
                Items = items,
                Page = 1,
                PageSize = items.Count,
                TotalCount = items.Count,
                TotalPages = 1
            });
        }

        if (unpaged == true)
        {
            var tickets = await repo.GetAllTicketsAsync(null, normalizedBoardId, visibilityContext);
            var ordered = QueryParameterValidation.IsSlaTicketListSort(normalizedSort)
                ? tickets
                : TicketQueryableExtensions.SortTicketEntitiesInMemory(tickets, normalizedSort);

            var mappingContext = await mappingContextFactory.CreateAsync(
                ordered.Select(ticket => ticket.CreatedBy),
                null,
                ordered.Select(ticket => ticket.BoardId));

            var responses = await MapTicketResponsesAsync(
                ordered,
                slaConfigurations,
                mappingContext,
                operationalRiskService,
                reassignmentRecommendationService,
                decisionImpactService);

            if (QueryParameterValidation.IsSlaTicketListSort(normalizedSort))
            {
                responses = SortTicketResponsesBySla(responses, normalizedSort);
            }

            return Results.Ok(new PagedTicketListResponse
            {
                Items = responses,
                Page = 1,
                PageSize = responses.Count,
                TotalCount = responses.Count,
                TotalPages = 1
            });
        }

        if (!QueryParameterValidation.TryNormalizeTicketListPage(page, out var normalizedPage, out var pageError))
        {
            return Results.BadRequest(new { message = pageError });
        }

        if (!QueryParameterValidation.TryNormalizeTicketListPageSize(pageSize, out var normalizedPageSize, out var pageSizeError))
        {
            return Results.BadRequest(new { message = pageSizeError });
        }

        if (QueryParameterValidation.IsSlaTicketListSort(normalizedSort))
        {
            var tickets = await repo.GetAllTicketsAsync(null, normalizedBoardId, visibilityContext);
            var mappingContext = await mappingContextFactory.CreateAsync(
                tickets.Select(ticket => ticket.CreatedBy),
                null,
                tickets.Select(ticket => ticket.BoardId));

            var responses = await MapTicketResponsesAsync(
                tickets,
                slaConfigurations,
                mappingContext,
                operationalRiskService,
                reassignmentRecommendationService,
                decisionImpactService);
            responses = SortTicketResponsesBySla(responses, normalizedSort);

            var totalCount = responses.Count;
            var pageItems = responses
                .Skip((normalizedPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToList();

            return Results.Ok(new PagedTicketListResponse
            {
                Items = pageItems,
                Page = normalizedPage,
                PageSize = normalizedPageSize,
                TotalCount = totalCount,
                TotalPages = ComputeTotalPages(totalCount, normalizedPageSize)
            });
        }

        var (pageTickets, total) = await repo.GetTicketsPageAsync(
            normalizedBoardId,
            visibilityContext,
            normalizedPage,
            normalizedPageSize,
            normalizedSort);

        var pageMappingContext = await mappingContextFactory.CreateAsync(
            pageTickets.Select(ticket => ticket.CreatedBy),
            null,
            pageTickets.Select(ticket => ticket.BoardId));

        var pagedResponses = await MapTicketResponsesAsync(
            pageTickets,
            slaConfigurations,
            pageMappingContext,
            operationalRiskService,
            reassignmentRecommendationService,
            decisionImpactService);

        return Results.Ok(new PagedTicketListResponse
        {
            Items = pagedResponses,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = total,
            TotalPages = ComputeTotalPages(total, normalizedPageSize)
        });
    }

    public static async Task<IResult> GetTicketBoardCounts(
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        CancellationToken cancellationToken)
    {
        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        var counts = await repo.GetActiveTicketBoardCountsAsync(
            visibilityContext,
            cancellationToken);

        var response = counts
            .Select(entry => new TicketBoardCountResponse
            {
                BoardId = entry.Key,
                Count = entry.Value
            })
            .OrderBy(entry => entry.BoardId)
            .ToList();

        return Results.Ok(response);
    }

    private static List<TicketResponse> SortTicketResponsesBySla(
        IReadOnlyList<TicketResponse> items,
        string sort)
    {
        if (sort == "due-soonest")
        {
            return items.OrderBy(r => r.SlaTargetDate).ThenBy(r => r.Id).ToList();
        }

        return items
            .OrderBy(r => r.SlaRemainingMinutes)
            .ThenBy(r => r.SlaTargetDate)
            .ThenBy(r => r.Id)
            .ToList();
    }

    private static int ComputeTotalPages(int totalCount, int pageSize) =>
        pageSize <= 0 ? 0 : (totalCount + pageSize - 1) / pageSize;

    private static async Task<List<TicketResponse>> MapTicketResponsesAsync(
        IReadOnlyList<Ticket> tickets,
        IReadOnlyDictionary<string, SlaConfiguration> slaConfigurations,
        ResponseMappingContext mappingContext,
        IOperationalRiskService operationalRiskService,
        IReassignmentRecommendationService reassignmentRecommendationService,
        IDecisionImpactService? decisionImpactService = null,
        CancellationToken cancellationToken = default)
    {
        var responses = tickets
            .Select(ticket => ticket.ToResponse(slaConfigurations, mappingContext))
            .ToList();
        if (responses.Count == 0)
        {
            return responses;
        }

        var riskByTicketId = await operationalRiskService.EvaluateBatchAsync(tickets, cancellationToken);
        var reassignmentByTicketId = await reassignmentRecommendationService.EvaluateBatchAsync(
            tickets,
            cancellationToken);
        IReadOnlyDictionary<string, DecisionImpactResponse> impactByTicketId = decisionImpactService is null
            ? new Dictionary<string, DecisionImpactResponse>(StringComparer.Ordinal)
            : await decisionImpactService.EvaluateBatchAsync(tickets, cancellationToken);
        foreach (var response in responses)
        {
            if (riskByTicketId.TryGetValue(response.Id, out var risk))
            {
                response.OperationalRisk = risk;
            }
            if (reassignmentByTicketId.TryGetValue(response.Id, out var reassignment))
            {
                response.ReassignmentRecommendation = reassignment;
            }
            if (impactByTicketId.TryGetValue(response.Id, out var impact))
            {
                response.DecisionImpact = impact;
            }
        }

        return responses;
    }

    private static async Task<TicketResponse> MapTicketResponseAsync(
        Ticket ticket,
        IReadOnlyDictionary<string, SlaConfiguration> slaConfigurations,
        ResponseMappingContext mappingContext,
        IOperationalRiskService operationalRiskService,
        IReassignmentRecommendationService reassignmentRecommendationService,
        IDecisionImpactService? decisionImpactService = null,
        CancellationToken cancellationToken = default)
    {
        var response = ticket.ToResponse(slaConfigurations, mappingContext);
        response.OperationalRisk = await operationalRiskService.EvaluateAsync(ticket, cancellationToken);
        response.ReassignmentRecommendation = await reassignmentRecommendationService.EvaluateAsync(
            ticket,
            cancellationToken);
        if (decisionImpactService is not null)
        {
            response.DecisionImpact = await decisionImpactService.EvaluateAsync(
                ticket,
                cancellationToken);
        }
        return response;
    }

    public static async Task<IResult> GetTicketById(
        string id,
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        ISlaConfigurationService slaConfigurationService,
        IResponseMappingContextFactory mappingContextFactory,
        [FromServices] IOperationalRiskService operationalRiskService,
        [FromServices] IReassignmentRecommendationService reassignmentRecommendationService,
        [FromServices] IDecisionImpactService decisionImpactService)
    {
        var ticket = await repo.GetTicketByIdAsync(id);
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();
        var mappingContext = await mappingContextFactory.CreateAsync(
            [ticket.CreatedBy],
            null,
            [ticket.BoardId]);

        return Results.Ok(await MapTicketResponseAsync(
            ticket,
            slaConfigurations,
            mappingContext,
            operationalRiskService,
            reassignmentRecommendationService,
            decisionImpactService));
    }

    public static async Task<IResult> GetTicketExternalSourceContext(
        string id,
        [FromServices] ITicketRepository repo,
        [FromServices] ITicketVisibilityService ticketVisibilityService,
        [FromServices] IExternalIntegrationService externalIntegrationService,
        CancellationToken cancellationToken)
    {
        var ticket = await repo.GetTicketByIdAsync(id.Trim());
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        var contexts = await externalIntegrationService.GetExternalSourceContextsForTicketAsync(
            ticket.Id,
            cancellationToken);
        return Results.Ok(contexts);
    }

    public static async Task<IResult> GetTicketSapReferenceContext(
        string id,
        [FromServices] ITicketRepository repo,
        [FromServices] ITicketVisibilityService ticketVisibilityService,
        [FromServices] ISapReferenceContextService sapReferenceContextService,
        CancellationToken cancellationToken)
    {
        var ticket = await repo.GetTicketByIdAsync(id.Trim());
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        var dto = await sapReferenceContextService.DetectSapReferencesForTicketAsync(
            ticket,
            cancellationToken);
        return Results.Ok(dto);
    }

    public static async Task<IResult> GetTicketSynitiKnowledgeContext(
        string id,
        [FromServices] ITicketRepository repo,
        [FromServices] ITicketVisibilityService ticketVisibilityService,
        [FromServices] ISynitiKnowledgeContextService synitiKnowledgeContextService,
        CancellationToken cancellationToken)
    {
        var ticket = await repo.GetTicketByIdAsync(id.Trim());
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        var dto = await synitiKnowledgeContextService.GetContextForTicketAsync(
            ticket,
            cancellationToken);
        return Results.Ok(dto);
    }

    public static async Task<IResult> GetTicketHistory(
        string id,
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        ITicketAuditService ticketAuditService,
        IResponseMappingContextFactory mappingContextFactory)
    {
        var ticketId = id.Trim();
        var ticket = await repo.GetTicketByIdAsync(ticketId);
        if (ticket is null)
        {
            var archivedTicket = await repo.GetArchivedTicketByIdAsync(ticketId);
            if (archivedTicket is null)
            {
                return Results.NotFound();
            }

            var archivedVisibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
            if (!archivedVisibilityContext.CanView(
                    archivedTicket.CreatedBy,
                    archivedTicket.SynitiOwner,
                    archivedTicket.BusinessOwner))
            {
                return Results.NotFound();
            }
        }
        else
        {
            var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
            if (!visibilityContext.CanView(ticket))
            {
                return Results.NotFound();
            }
        }

        var history = (await ticketAuditService.GetTicketHistoryAsync(ticketId)).ToList();
        var mappingContext = await mappingContextFactory.CreateAsync(
            history.Select(entry => entry.ChangedBy));
        return Results.Ok(history.Select(entry => entry.ToResponse(mappingContext)));
    }

    public static async Task<IResult> GetLatestRoutingDecision(
        string id,
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        ITicketRoutingRuleService ticketRoutingRuleService,
        ILogger<TicketHandlersLogCategory> logger)
    {
        var ticket = await repo.GetTicketByIdAsync(id);
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        try
        {
            var decision = await ticketRoutingRuleService.GetLatestDecisionAsync(id);
            var @override = await ticketRoutingRuleService.GetLatestOverrideAsync(id);
            return Results.Ok(new TicketRoutingLatestResponse
            {
                Decision = decision?.ToResponse(),
                Override = @override?.ToResponse()
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Unable to load optional routing details for ticket {TicketId}. Returning empty routing details.",
                id);
            return Results.Ok(new TicketRoutingLatestResponse());
        }
    }

    public static async Task<IResult> GetTicketDecision(
        string id,
        [FromServices] ITicketRepository repo,
        [FromServices] ITicketVisibilityService ticketVisibilityService,
        [FromServices] ICortexDecisionService cortexDecisionService,
        [FromServices] ICortexAiAssessmentService cortexAiAssessmentService,
        CancellationToken cancellationToken)
    {
        var ticket = await repo.GetTicketByIdAsync(id.Trim());
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        var assessment = await cortexAiAssessmentService.AssessTicketAsync(ticket, cancellationToken);
        var decision = await cortexDecisionService.EvaluateAssignmentAsync(ticket, assessment, cancellationToken);
        return Results.Ok(decision);
    }

    public static async Task<IResult> GetTicketAutonomy(
        string id,
        [FromServices] ITicketRepository repo,
        [FromServices] ITicketVisibilityService ticketVisibilityService,
        [FromServices] ICortexAutonomyService cortexAutonomyService,
        CancellationToken cancellationToken)
    {
        var ticket = await repo.GetTicketByIdAsync(id.Trim());
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        var latest = await cortexAutonomyService.GetLatestAsync(ticket.Id, cancellationToken);
        return latest is null ? Results.NoContent() : Results.Ok(latest);
    }

    public static async Task<IResult> EvaluateTicketAutonomy(
        string id,
        [FromServices] ITicketRepository repo,
        [FromServices] ITicketVisibilityService ticketVisibilityService,
        [FromServices] ICortexAutonomyService cortexAutonomyService,
        CancellationToken cancellationToken)
    {
        var ticket = await repo.GetTicketByIdAsync(id.Trim());
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        var result = await cortexAutonomyService.EvaluateAndMaybeApplyDecisionAsync(ticket, cancellationToken);
        return Results.Ok(result);
    }

    public static async Task<IResult> GetTicketInsight(
        string id,
        [FromServices] ITicketRepository repo,
        [FromServices] ITicketVisibilityService ticketVisibilityService,
        [FromServices] ICortexInsightService cortexInsightService,
        CancellationToken cancellationToken)
    {
        var ticket = await repo.GetTicketByIdAsync(id.Trim());
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        var insight = await cortexInsightService.GetInsightAsync(
            ticket,
            visibilityContext,
            cancellationToken);
        return Results.Ok(insight);
    }

    public static async Task<IResult> GetTicketCachedInsight(
        string id,
        [FromServices] ITicketRepository repo,
        [FromServices] ITicketVisibilityService ticketVisibilityService,
        [FromServices] ICortexInsightService cortexInsightService)
    {
        var ticket = await repo.GetTicketByIdAsync(id.Trim());
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        return cortexInsightService.TryGetCachedInsight(
            ticket.Id,
            visibilityContext,
            out var cachedInsight)
            ? Results.Ok(cachedInsight)
            : Results.NoContent();
    }

    public static async Task<IResult> PostOwnerWorkloadPreview(
        OwnerWorkloadPreviewRequest? request,
        IOwnerWorkloadPreviewService workloadPreviewService)
    {
        var body = request ?? new OwnerWorkloadPreviewRequest();
        var result = await workloadPreviewService.GetSummariesAsync(body);
        return Results.Ok(result);
    }

    public static async Task<IResult> GetTicketRisk(
        string id,
        [FromServices] ITicketRepository repo,
        [FromServices] ITicketVisibilityService ticketVisibilityService,
        [FromServices] ICortexSlaRiskService cortexSlaRiskService,
        [FromServices] ICortexInsightService cortexInsightService,
        [FromServices] ITicketOutcomeService? ticketOutcomeService,
        CancellationToken cancellationToken)
    {
        var ticket = await repo.GetTicketByIdAsync(id.Trim());
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        cortexInsightService.TryGetCachedInsight(ticket.Id, visibilityContext, out var cachedInsight);
        var assessment = await cortexSlaRiskService.EvaluateRiskAsync(
            ticket,
            cancellationToken,
            cachedInsight);
        if (ticketOutcomeService is not null && IsBreachedSlaStatus(assessment.SlaStatus))
        {
            await ticketOutcomeService.MarkSlaBreachedAsync(ticket, cancellationToken);
        }

        return Results.Ok(new CortexSlaRiskResponse
        {
            TicketId = ticket.Id,
            RiskLevel = assessment.RiskLevel.ToString(),
            RiskReasons = assessment.RiskReasons,
            Recommendation = HumanizeRecommendation(assessment.Recommendation),
            RecommendationReason = assessment.RecommendationReason,
            Confidence = assessment.Confidence,
            SlaStatus = assessment.SlaStatus,
            EvaluatedAtUtc = DateTime.UtcNow
        });
    }

    private static string HumanizeRecommendation(CortexRiskRecommendation recommendation) =>
        recommendation switch
        {
            CortexRiskRecommendation.RequestMoreDetail => "Request more detail",
            CortexRiskRecommendation.Reassign => "Reassign",
            CortexRiskRecommendation.Escalate => "Escalate",
            _ => "Keep on current path"
        };

    private static bool IsBreachedSlaStatus(string? slaStatus) =>
        slaStatus is not null
        && (slaStatus.Equals("Breached", StringComparison.OrdinalIgnoreCase)
            || slaStatus.Equals("Resolved Late", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Evaluates routing rules from draft field values without persisting (ticket modal live preview).
    /// Requester department/role come from the ticket creator, matching update-ticket routing behavior.
    /// </summary>
    public static async Task<IResult> PostRoutingPreview(
        RoutingPreviewRequest? request,
        ITicketRepository repo,
        IUserRepository userRepository,
        ITicketVisibilityService ticketVisibilityService,
        ITicketRoutingRuleService ticketRoutingRuleService)
    {
        var body = request ?? new RoutingPreviewRequest();
        if (string.IsNullOrWhiteSpace(body.TicketId))
        {
            return Results.BadRequest(new { message = "TicketId is required." });
        }

        if (string.IsNullOrWhiteSpace(body.Priority))
        {
            return Results.BadRequest(new { message = "Priority is required." });
        }

        var ticket = await repo.GetTicketByIdAsync(body.TicketId.Trim());
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        string normalizedPriority;
        try
        {
            normalizedPriority = NormalizePriority(body.Priority);
        }
        catch (ArgumentException ex)
        {
            return Results.BadRequest(new { message = ex.Message });
        }

        var requester = await userRepository.GetByIdAsync(ticket.CreatedBy);
        var requesterDepartment = requester?.Department;
        var requesterRole = requester?.Role;
        var legacyDepartment = body.Department ?? requesterDepartment;
        var title = string.IsNullOrWhiteSpace(body.Title)
            ? ticket.Title
            : body.Title.Trim();

        var factors = BuildRoutingFactors(
            body.BoardId,
            normalizedPriority,
            requesterDepartment,
            requesterRole,
            legacyDepartment,
            title);

        var result = await ticketRoutingRuleService.EvaluateAsync(factors, body.TicketId.Trim());
        return Results.Ok(new RoutingPreviewResponse
        {
            Decision = result.ToPreviewResponse(ticket.Id)
        });
    }

    public static async Task<IResult> ApplyGuidedReassignment(
        string id,
        ReassignmentApplyRequest? request,
        HttpContext httpContext,
        ITicketRepository repo,
        IUserContextService userContext,
        ITicketVisibilityService ticketVisibilityService,
        ISlaConfigurationService slaConfigurationService,
        ITicketAuditService ticketAuditService,
        ITicketRoutingRuleService ticketRoutingRuleService,
        IRealtimeEventService realtimeEventService,
        IRealtimeAudienceResolver realtimeAudienceResolver,
        IResponseMappingContextFactory mappingContextFactory,
        [FromServices] IOperationalRiskService operationalRiskService,
        [FromServices] IReassignmentRecommendationService reassignmentRecommendationService,
        [FromServices] IReassignmentExecutionService reassignmentExecutionService,
        [FromServices] IDecisionImpactService decisionImpactService,
        [FromServices] ITicketOutcomeService? ticketOutcomeService = null)
    {
        var body = request ?? new ReassignmentApplyRequest();
        if (!string.IsNullOrWhiteSpace(body.TicketId)
            && !string.Equals(body.TicketId.Trim(), id.Trim(), StringComparison.Ordinal))
        {
            return Results.BadRequest(new { message = "TicketId does not match route id." });
        }

        if (!HasBusinessTicketEditRole(httpContext.User))
        {
            return Results.Json(
                new { message = "You do not have permission to update ticket ownership." },
                statusCode: StatusCodes.Status403Forbidden);
        }

        var ticket = await repo.GetTicketByIdAsync(id.Trim());
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        if (!string.IsNullOrWhiteSpace(body.ConcurrencyToken))
        {
            byte[] incomingRowVersion;
            try
            {
                incomingRowVersion = Convert.FromBase64String(body.ConcurrencyToken.Trim());
            }
            catch (FormatException)
            {
                return Results.BadRequest(new { message = "Invalid concurrency token." });
            }

            if (ticket.RowVersion is not { Length: > 0 }
                || !incomingRowVersion.AsSpan().SequenceEqual(ticket.RowVersion))
            {
                return Results.Conflict(new
                {
                    message =
                        "Ticket assignment changed before reassignment could be applied.",
                });
            }
        }

        var originalTicket = CloneTicket(ticket);
        var currentUser = await userContext.GetCurrentUserAsync();
        var executionResult = await reassignmentExecutionService.ExecuteAsync(
            ticket,
            body,
            currentUser);
        if (!executionResult.Succeeded)
        {
            return Results.Json(
                new { message = executionResult.Message },
                statusCode: executionResult.StatusCode);
        }

        await repo.UpdateTicketAsync(ticket);
        try
        {
            await repo.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new
            {
                message = "Ticket assignment changed before reassignment could be applied.",
            });
        }

        var auditReason = string.IsNullOrWhiteSpace(body.Reason)
            ? "Selected from suggested reassignment targets."
            : body.Reason.Trim();
        var auditMessage =
            $"Owner reassigned from {executionResult.PreviousOwner} to {executionResult.NewOwner} using Cortex recommendation review flow.";

        await ticketAuditService.RecordTicketUpdatedAsync(
            originalTicket,
            ticket,
            currentUser,
            $"{auditReason} Source={executionResult.ReassignmentSource}");

        if (executionResult.AssignmentField == "synitiOwner")
        {
            await ticketRoutingRuleService.RecordOverrideAsync(
                ticket.Id,
                currentUser.Id,
                executionResult.PreviousOwner,
                ticket.BusinessOwner,
                executionResult.NewOwner,
                ticket.BusinessOwner,
                RoutingOverrideReasonType.WorkloadAdjustment,
                $"{auditReason} ({executionResult.ReassignmentSource})",
                executionResult.DecisionImpactSnapshot);
        }
        else if (executionResult.AssignmentField == "businessOwner")
        {
            await ticketRoutingRuleService.RecordOverrideAsync(
                ticket.Id,
                currentUser.Id,
                ticket.SynitiOwner,
                executionResult.PreviousOwner,
                ticket.SynitiOwner,
                executionResult.NewOwner,
                RoutingOverrideReasonType.WorkloadAdjustment,
                $"{auditReason} ({executionResult.ReassignmentSource})",
                executionResult.DecisionImpactSnapshot);
        }

        var refreshedTicket = await repo.GetTicketByIdAsync(ticket.Id);
        if (refreshedTicket is null)
        {
            return Results.Problem("Ticket was updated but could not be retrieved.");
        }

        if (ticketOutcomeService is not null)
        {
            if (HasMeaningfulOwnerChange(originalTicket.SynitiOwner, refreshedTicket.SynitiOwner))
            {
                await ticketOutcomeService.MarkReassignedAsync(
                    refreshedTicket,
                    originalTicket.SynitiOwner,
                    CancellationToken.None);
            }

            await ticketOutcomeService.RecordOverrideAsync(
                refreshedTicket.Id,
                refreshedTicket.SynitiOwner,
                refreshedTicket.BusinessOwner,
                CancellationToken.None);
        }

        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();
        var mappingContext = await mappingContextFactory.CreateAsync(
            [refreshedTicket.CreatedBy],
            null,
            [refreshedTicket.BoardId]);
        var responseTicket = await MapTicketResponseAsync(
            refreshedTicket,
            slaConfigurations,
            mappingContext,
            operationalRiskService,
            reassignmentRecommendationService,
            decisionImpactService);
        var audienceUserIds = await realtimeAudienceResolver.GetAudienceUserIdsAsync(refreshedTicket);
        await realtimeEventService.PublishAsync(new RealtimeEventMessage
        {
            EventType = "ticket.updated",
            TicketId = refreshedTicket.Id,
            EntityId = refreshedTicket.Id,
            AudienceUserIds = audienceUserIds,
            Ticket = responseTicket,
        });

        return Results.Ok(new ReassignmentApplyResponse
        {
            TicketId = refreshedTicket.Id,
            PreviousOwner = executionResult.PreviousOwner ?? string.Empty,
            NewOwner = executionResult.NewOwner ?? string.Empty,
            Applied = true,
            AppliedAtUtc = DateTime.UtcNow,
            AuditMessage = auditMessage,
            ReassignmentSource = executionResult.ReassignmentSource,
            Ticket = responseTicket,
        });
    }

    public static async Task<IResult> GetArchivedTickets(
        DateTimeOffset? sinceUtc,
        int? boardId,
        int? page,
        int? pageSize,
        bool? unpaged,
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        IResponseMappingContextFactory mappingContextFactory)
    {
        if (!QueryParameterValidation.TryValidateOptionalBoardId(boardId, out var normalizedBoardId, out var boardIdError))
        {
            return Results.BadRequest(new { message = boardIdError });
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();

        if (sinceUtc.HasValue)
        {
            var archivedTickets = await repo.GetArchivedTicketsAsync(
                sinceUtc.Value.UtcDateTime,
                normalizedBoardId,
                visibilityContext);

            var mappingContext = await mappingContextFactory.CreateAsync(
                archivedTickets.SelectMany(ticket => new[] { ticket.CreatedBy, ticket.ArchivedBy }),
                null,
                archivedTickets.Select(ticket => ticket.BoardId));

            var items = archivedTickets
                .Select(ticket => ticket.ToResponse(mappingContext))
                .ToList();

            return Results.Ok(new PagedArchivedTicketListResponse
            {
                Items = items,
                Page = 1,
                PageSize = items.Count,
                TotalCount = items.Count,
                TotalPages = 1
            });
        }

        if (unpaged == true)
        {
            var archivedTickets = await repo.GetArchivedTicketsAsync(null, normalizedBoardId, visibilityContext);

            var mappingContext = await mappingContextFactory.CreateAsync(
                archivedTickets.SelectMany(ticket => new[] { ticket.CreatedBy, ticket.ArchivedBy }),
                null,
                archivedTickets.Select(ticket => ticket.BoardId));

            var responses = archivedTickets
                .Select(ticket => ticket.ToResponse(mappingContext))
                .ToList();

            return Results.Ok(new PagedArchivedTicketListResponse
            {
                Items = responses,
                Page = 1,
                PageSize = responses.Count,
                TotalCount = responses.Count,
                TotalPages = 1
            });
        }

        if (!QueryParameterValidation.TryNormalizeTicketListPage(page, out var normalizedPage, out var pageError))
        {
            return Results.BadRequest(new { message = pageError });
        }

        if (!QueryParameterValidation.TryNormalizeTicketListPageSize(pageSize, out var normalizedPageSize, out var pageSizeError))
        {
            return Results.BadRequest(new { message = pageSizeError });
        }

        var (pageTickets, total) = await repo.GetArchivedTicketsPageAsync(
            normalizedBoardId,
            visibilityContext,
            normalizedPage,
            normalizedPageSize);

        var pageMappingContext = await mappingContextFactory.CreateAsync(
            pageTickets.SelectMany(ticket => new[] { ticket.CreatedBy, ticket.ArchivedBy }),
            null,
            pageTickets.Select(ticket => ticket.BoardId));

        var pagedResponses = pageTickets
            .Select(ticket => ticket.ToResponse(pageMappingContext))
            .ToList();

        return Results.Ok(new PagedArchivedTicketListResponse
        {
            Items = pagedResponses,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = total,
            TotalPages = ComputeTotalPages(total, normalizedPageSize)
        });
    }

    public static async Task<IResult> GetTicketsByStatus(
        string status,
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        ITicketStatusService ticketStatusService,
        ISlaConfigurationService slaConfigurationService,
        IResponseMappingContextFactory mappingContextFactory,
        [FromServices] IOperationalRiskService operationalRiskService,
        [FromServices] IReassignmentRecommendationService reassignmentRecommendationService)
    {
        if (!QueryParameterValidation.TryValidateSafeFilterString(status, out var trimmedStatus, out var statusError))
        {
            return Results.BadRequest(new { message = statusError });
        }

        var definitions = await ticketStatusService.GetAllAsync();
        if (definitions.Count > 0)
        {
            var match = definitions.FirstOrDefault(definition =>
                string.Equals(definition.Name, trimmedStatus, StringComparison.OrdinalIgnoreCase));
            if (match is null)
            {
                return Results.BadRequest(new { message = "Status must be a configured ticket status name." });
            }

            trimmedStatus = match.Name;
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        var filtered = await repo.GetTicketsByStatusAsync(trimmedStatus);
        var visibleTickets = filtered.Where(visibilityContext.CanView).ToList();

        if (!visibleTickets.Any())
        {
            return Results.NotFound();
        }

        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();
        var mappingContext = await mappingContextFactory.CreateAsync(
            visibleTickets.Select(ticket => ticket.CreatedBy),
            null,
            visibleTickets.Select(ticket => ticket.BoardId));

        var responses = await MapTicketResponsesAsync(
            visibleTickets,
            slaConfigurations,
            mappingContext,
            operationalRiskService,
            reassignmentRecommendationService);
        return Results.Ok(responses);
    }

    public static async Task<IResult> GetTicketsByPriority(
        string priority,
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        ISlaConfigurationService slaConfigurationService,
        IResponseMappingContextFactory mappingContextFactory,
        [FromServices] IOperationalRiskService operationalRiskService,
        [FromServices] IReassignmentRecommendationService reassignmentRecommendationService)
    {
        if (!QueryParameterValidation.TryValidateSafeFilterString(priority, out var trimmedPriority, out var priorityError))
        {
            return Results.BadRequest(new { message = priorityError });
        }

        if (!QueryParameterValidation.IsAllowedPriority(trimmedPriority, AllowedPriorities, out var canonicalPriority))
        {
            return Results.BadRequest(new
            {
                message = $"Priority must be one of: {string.Join(", ", AllowedPriorities)}."
            });
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        var filtered = await repo.GetTicketsByPriorityAsync(canonicalPriority);
        var visibleTickets = filtered.Where(visibilityContext.CanView).ToList();

        if (!visibleTickets.Any())
        {
            return Results.NotFound();
        }

        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();
        var mappingContext = await mappingContextFactory.CreateAsync(
            visibleTickets.Select(ticket => ticket.CreatedBy),
            null,
            visibleTickets.Select(ticket => ticket.BoardId));

        var responses = await MapTicketResponsesAsync(
            visibleTickets,
            slaConfigurations,
            mappingContext,
            operationalRiskService,
            reassignmentRecommendationService);
        return Results.Ok(responses);
    }

    public static async Task<IResult> GetTicketsByUser(
        IUserContextService userContext,
        ITicketRepository repo,
        ISlaConfigurationService slaConfigurationService,
        IResponseMappingContextFactory mappingContextFactory,
        [FromServices] IOperationalRiskService operationalRiskService,
        [FromServices] IReassignmentRecommendationService reassignmentRecommendationService)
    {
        var currentUser = await userContext.GetCurrentUserAsync();
        var tickets = (await repo.GetTicketByUserAsync(currentUser)).ToList();
        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();

        var mappingContext = await mappingContextFactory.CreateAsync(
            tickets.Select(ticket => ticket.CreatedBy),
            null,
            tickets.Select(ticket => ticket.BoardId));

        var responses = await MapTicketResponsesAsync(
            tickets,
            slaConfigurations,
            mappingContext,
            operationalRiskService,
            reassignmentRecommendationService);
        return Results.Ok(responses);
    }

    public static async Task<IResult> GetTicketsPendingApproval(
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        ISlaConfigurationService slaConfigurationService,
        IResponseMappingContextFactory mappingContextFactory,
        [FromServices] IOperationalRiskService operationalRiskService,
        [FromServices] IReassignmentRecommendationService reassignmentRecommendationService)
    {
        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        var tickets = await repo.GetIntakeQueueTicketsAsync(visibilityContext);
        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();
        var mappingContext = await mappingContextFactory.CreateAsync(
            tickets.Select(ticket => ticket.CreatedBy),
            null,
            tickets.Select(ticket => ticket.BoardId));
        var items = await MapTicketResponsesAsync(
            tickets,
            slaConfigurations,
            mappingContext,
            operationalRiskService,
            reassignmentRecommendationService);

        return Results.Ok(new PagedTicketListResponse
        {
            Items = items,
            Page = 1,
            PageSize = items.Count,
            TotalCount = items.Count,
            TotalPages = 1
        });
    }

    public static async Task<IResult> ApproveTicket(
        string id,
        TicketApprovalActionRequest? _,
        ITicketRepository repo,
        IUserContextService userContext,
        IUserRepository userRepository,
        ITicketVisibilityService ticketVisibilityService,
        ISlaConfigurationService slaConfigurationService,
        ITicketRoutingRuleService ticketRoutingRuleService,
        [FromServices] IOperationalRiskService operationalRiskService,
        [FromServices] IReassignmentRecommendationService reassignmentRecommendationService,
        INotificationService notificationService,
        IRealtimeEventService realtimeEventService,
        IRealtimeAudienceResolver realtimeAudienceResolver,
        IResponseMappingContextFactory mappingContextFactory,
        ILogger<TicketHandlersLogCategory> logger,
        [FromServices] ITicketOutcomeService? ticketOutcomeService = null)
    {
        var ticket = await repo.GetTicketByIdAsync(id.Trim());
        if (ticket is null)
        {
            return Results.NotFound();
        }

        if (ticket.ApprovalStatus != ApprovalStatus.PendingApproval)
        {
            return Results.Conflict(new
            {
                message = "Only tickets awaiting approval can be approved."
            });
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        var currentUser = await userContext.GetCurrentUserAsync();
        var requester = await userRepository.GetByIdAsync(ticket.CreatedBy);
        var requesterDepartment = requester?.Department;
        var requesterRole = requester?.Role;
        var hadManualSynitiOwner = !string.IsNullOrWhiteSpace(ticket.SynitiOwner);
        var hadManualBusinessOwner = !string.IsNullOrWhiteSpace(ticket.BusinessOwner);
        var approvedPriority = ResolveApprovedPriority(ticket.Priority, ticket.AiTriageSuggestedPriority);
        ticket.Priority = approvedPriority;
        logger.LogInformation(
            "[APPROVAL-ROUTING] Ticket {TicketId} approval start. Priority={Priority}. ExistingSynitiOwner={ExistingSynitiOwner}. ExistingBusinessOwner={ExistingBusinessOwner}.",
            ticket.Id,
            approvedPriority,
            ticket.SynitiOwner,
            ticket.BusinessOwner);
        var routingDecision = await ticketRoutingRuleService.EvaluateAsync(
            BuildRoutingFactors(
                ticket.BoardId,
                approvedPriority,
                requesterDepartment,
                requesterRole,
                requesterDepartment,
                ticket.Title),
            ticket.Id);
        var latestDecision = await ticketRoutingRuleService.GetLatestDecisionAsync(ticket.Id);
        var recommendedSynitiOwner = NormalizeOptionalValue(routingDecision.RecommendedSynitiOwner)
            ?? TryExtractRecommendedOwnerFromDecisionExplanation(routingDecision.ExplanationJson, "synitiOwner")
            ?? NormalizeOptionalValue(latestDecision?.ChosenSynitiOwner)
            ?? TryExtractRecommendedOwnerFromDecisionExplanation(latestDecision?.ExplanationJson, "synitiOwner");
        var recommendedBusinessOwner = NormalizeOptionalValue(routingDecision.RecommendedBusinessOwner)
            ?? TryExtractRecommendedOwnerFromDecisionExplanation(routingDecision.ExplanationJson, "businessOwner")
            ?? NormalizeOptionalValue(latestDecision?.ChosenBusinessOwner)
            ?? TryExtractRecommendedOwnerFromDecisionExplanation(latestDecision?.ExplanationJson, "businessOwner");
        logger.LogInformation(
            "[APPROVAL-ROUTING] Ticket {TicketId} recommendation resolved. RecommendedSynitiOwner={RecommendedSynitiOwner}. RecommendedBusinessOwner={RecommendedBusinessOwner}.",
            ticket.Id,
            recommendedSynitiOwner,
            recommendedBusinessOwner);

        var originalForAssignment = CloneTicket(ticket);
        var resolvedSynitiOwner = hadManualSynitiOwner
            ? ticket.SynitiOwner
            : recommendedSynitiOwner ?? ticket.SynitiOwner;
        var resolvedBusinessOwner = hadManualBusinessOwner
            ? ticket.BusinessOwner
            : recommendedBusinessOwner
                ?? ticket.BusinessOwner
                ?? (requester is not null ? GetDefaultBusinessOwner(requester) : null)
            ;

        try
        {
            var normalizedOwners = await TicketOwnerAssignmentValidation.NormalizeAndValidateAsync(
                userRepository,
                resolvedSynitiOwner,
                resolvedBusinessOwner);
            resolvedSynitiOwner = normalizedOwners.SynitiOwner;
            resolvedBusinessOwner = normalizedOwners.BusinessOwner;
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }

        ticket.SynitiOwner = resolvedSynitiOwner;
        ticket.BusinessOwner = resolvedBusinessOwner;
        ticket.Status = "In Progress";
        ticket.ApprovalStatus = ApprovalStatus.Approved;
        ticket.ApprovedAt = DateTime.UtcNow;
        ticket.ApprovedBy = currentUser.Id;
        ticket.RejectedAt = null;
        ticket.RejectedBy = null;
        ticket.RejectionReason = null;
        ticket.ReturnedForDetailAt = null;
        ticket.ReturnedForDetailBy = null;
        ticket.ReturnReason = null;
        ticket.LastModifiedBy = currentUser.Id;
        ticket.LastModifiedDate = DateTime.UtcNow;

        await ticketRoutingRuleService.RecordDecisionAsync(ticket.Id, routingDecision);
        var synitiOwnerManuallyOverridden = hadManualSynitiOwner
            && !OwnerFieldsEqual(recommendedSynitiOwner, resolvedSynitiOwner);
        var businessOwnerManuallyOverridden = hadManualBusinessOwner
            && !OwnerFieldsEqual(recommendedBusinessOwner, resolvedBusinessOwner);
        var approvalOwnerOverridden = synitiOwnerManuallyOverridden || businessOwnerManuallyOverridden;
        logger.LogInformation(
            "[APPROVAL-ROUTING] Ticket {TicketId} final owners. SynitiOwner={SynitiOwner}. BusinessOwner={BusinessOwner}. ManualOverride={ManualOverride}.",
            ticket.Id,
            resolvedSynitiOwner,
            resolvedBusinessOwner,
            approvalOwnerOverridden);
        if (approvalOwnerOverridden)
        {
            await ticketRoutingRuleService.RecordOverrideAsync(
                ticketId: ticket.Id,
                overriddenByUserId: currentUser.Id,
                previousSynitiOwner: recommendedSynitiOwner,
                previousBusinessOwner: recommendedBusinessOwner,
                newSynitiOwner: resolvedSynitiOwner,
                newBusinessOwner: resolvedBusinessOwner,
                reasonType: RoutingOverrideReasonType.Other,
                reasonText: "Approval routing");
        }

        await repo.UpdateTicketAsync(ticket);
        try
        {
            await repo.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new
            {
                message =
                    "This ticket was updated elsewhere. Refresh the page to load the latest version before trying again.",
            });
        }

        var updatedTicket = await repo.GetTicketByIdAsync(ticket.Id);
        if (updatedTicket is null)
        {
            return Results.Problem("Ticket was approved but could not be retrieved.");
        }
        logger.LogInformation(
            "[APPROVAL-ROUTING] Ticket {TicketId} persisted owners. SynitiOwner={SynitiOwner}. BusinessOwner={BusinessOwner}. ApprovalStatus={ApprovalStatus}.",
            updatedTicket.Id,
            updatedTicket.SynitiOwner,
            updatedTicket.BusinessOwner,
            updatedTicket.ApprovalStatus);

        if (ticketOutcomeService is not null)
        {
            await ticketOutcomeService.RecordInitialAssignmentAsync(
                updatedTicket,
                routingDecision.MatchedRuleId,
                CancellationToken.None);
            if (approvalOwnerOverridden)
            {
                await ticketOutcomeService.RecordOverrideAsync(
                    updatedTicket.Id,
                    resolvedSynitiOwner,
                    resolvedBusinessOwner,
                    CancellationToken.None);
            }
        }

        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();
        var mappingContext = await mappingContextFactory.CreateAsync(
            [updatedTicket.CreatedBy],
            null,
            [updatedTicket.BoardId]);
        var response = await MapTicketResponseAsync(
            updatedTicket,
            slaConfigurations,
            mappingContext,
            operationalRiskService,
            reassignmentRecommendationService);
        logger.LogInformation(
            "[APPROVAL-ROUTING] Ticket {TicketId} response owners. SynitiOwner={SynitiOwner}. BusinessOwner={BusinessOwner}.",
            response.Id,
            response.SynitiOwner,
            response.BusinessOwner);
        var audienceUserIds = await realtimeAudienceResolver.GetAudienceUserIdsAsync(updatedTicket);

        await notificationService.CreateAssignmentNotificationsAsync(
            originalForAssignment,
            updatedTicket,
            currentUser);

        await realtimeEventService.PublishAsync(new RealtimeEventMessage
        {
            EventType = "ticket.updated",
            TicketId = updatedTicket.Id,
            EntityId = updatedTicket.Id,
            AudienceUserIds = audienceUserIds,
            Ticket = response
        });
        await realtimeEventService.PublishAsync(new RealtimeEventMessage
        {
            EventType = "ticket.routed",
            TicketId = updatedTicket.Id,
            EntityId = updatedTicket.Id,
            AudienceUserIds = audienceUserIds,
            Ticket = response
        });

        LogTicketApproved(logger, updatedTicket.Id, currentUser.Id);
        return Results.Ok(response);
    }

    public static async Task<IResult> ReturnTicketForDetail(
        string id,
        TicketApprovalActionRequest? request,
        ITicketRepository repo,
        IUserContextService userContext,
        ITicketVisibilityService ticketVisibilityService,
        ISlaConfigurationService slaConfigurationService,
        IResponseMappingContextFactory mappingContextFactory,
        [FromServices] IOperationalRiskService operationalRiskService,
        [FromServices] IReassignmentRecommendationService reassignmentRecommendationService,
        IRealtimeEventService realtimeEventService,
        IRealtimeAudienceResolver realtimeAudienceResolver,
        [FromServices] ITicketOutcomeService? ticketOutcomeService = null)
    {
        var reason = request?.Reason?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(reason))
        {
            return Results.BadRequest(new { message = "Return reason is required." });
        }

        if (reason.Length > MaxApprovalReasonLength)
        {
            return Results.BadRequest(new
            {
                message = $"Return reason must be {MaxApprovalReasonLength} characters or fewer."
            });
        }

        var ticket = await repo.GetTicketByIdAsync(id.Trim());
        if (ticket is null)
        {
            return Results.NotFound();
        }

        if (ticket.ApprovalStatus != ApprovalStatus.PendingApproval)
        {
            return Results.Conflict(new
            {
                message = "Only tickets awaiting approval can be returned for detail."
            });
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        var currentUser = await userContext.GetCurrentUserAsync();
        ticket.ApprovalStatus = ApprovalStatus.NeedsMoreInfo;
        ticket.ReturnedForDetailAt = DateTime.UtcNow;
        ticket.ReturnedForDetailBy = currentUser.Id;
        ticket.ReturnReason = reason;
        ticket.LastModifiedBy = currentUser.Id;
        ticket.LastModifiedDate = DateTime.UtcNow;

        await repo.UpdateTicketAsync(ticket);
        try
        {
            await repo.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new
            {
                message =
                    "This ticket was updated elsewhere. Refresh the page to load the latest version before trying again.",
            });
        }

        var updatedTicket = await repo.GetTicketByIdAsync(ticket.Id);
        if (updatedTicket is null)
        {
            return Results.Problem("Ticket was updated but could not be retrieved.");
        }

        if (ticketOutcomeService is not null)
        {
            await ticketOutcomeService.MarkReturnedForDetailAsync(
                updatedTicket,
                CancellationToken.None);
        }

        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();
        var mappingContext = await mappingContextFactory.CreateAsync(
            [updatedTicket.CreatedBy],
            null,
            [updatedTicket.BoardId]);
        var response = await MapTicketResponseAsync(
            updatedTicket,
            slaConfigurations,
            mappingContext,
            operationalRiskService,
            reassignmentRecommendationService);
        var audienceUserIds = await realtimeAudienceResolver.GetAudienceUserIdsAsync(updatedTicket);

        await realtimeEventService.PublishAsync(new RealtimeEventMessage
        {
            EventType = "ticket.updated",
            TicketId = updatedTicket.Id,
            EntityId = updatedTicket.Id,
            AudienceUserIds = audienceUserIds,
            Ticket = response
        });

        return Results.Ok(response);
    }

    public static async Task<IResult> RejectTicket(
        string id,
        TicketApprovalActionRequest? request,
        ITicketRepository repo,
        IUserContextService userContext,
        ITicketVisibilityService ticketVisibilityService,
        ISlaConfigurationService slaConfigurationService,
        IResponseMappingContextFactory mappingContextFactory,
        [FromServices] IOperationalRiskService operationalRiskService,
        [FromServices] IReassignmentRecommendationService reassignmentRecommendationService,
        IRealtimeEventService realtimeEventService,
        IRealtimeAudienceResolver realtimeAudienceResolver)
    {
        var reason = request?.Reason?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(reason))
        {
            return Results.BadRequest(new { message = "Rejection reason is required." });
        }

        if (reason.Length > MaxApprovalReasonLength)
        {
            return Results.BadRequest(new
            {
                message = $"Rejection reason must be {MaxApprovalReasonLength} characters or fewer."
            });
        }

        var ticket = await repo.GetTicketByIdAsync(id.Trim());
        if (ticket is null)
        {
            return Results.NotFound();
        }

        if (ticket.ApprovalStatus != ApprovalStatus.PendingApproval)
        {
            return Results.Conflict(new
            {
                message = "Only tickets awaiting approval can be rejected."
            });
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        var currentUser = await userContext.GetCurrentUserAsync();
        ticket.ApprovalStatus = ApprovalStatus.Rejected;
        ticket.RejectedAt = DateTime.UtcNow;
        ticket.RejectedBy = currentUser.Id;
        ticket.RejectionReason = reason;
        ticket.LastModifiedBy = currentUser.Id;
        ticket.LastModifiedDate = DateTime.UtcNow;

        await repo.UpdateTicketAsync(ticket);
        try
        {
            await repo.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new
            {
                message =
                    "This ticket was updated elsewhere. Refresh the page to load the latest version before trying again.",
            });
        }

        var updatedTicket = await repo.GetTicketByIdAsync(ticket.Id);
        if (updatedTicket is null)
        {
            return Results.Problem("Ticket was updated but could not be retrieved.");
        }

        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();
        var mappingContext = await mappingContextFactory.CreateAsync(
            [updatedTicket.CreatedBy],
            null,
            [updatedTicket.BoardId]);
        var response = await MapTicketResponseAsync(
            updatedTicket,
            slaConfigurations,
            mappingContext,
            operationalRiskService,
            reassignmentRecommendationService);
        var audienceUserIds = await realtimeAudienceResolver.GetAudienceUserIdsAsync(updatedTicket);

        await realtimeEventService.PublishAsync(new RealtimeEventMessage
        {
            EventType = "ticket.updated",
            TicketId = updatedTicket.Id,
            EntityId = updatedTicket.Id,
            AudienceUserIds = audienceUserIds,
            Ticket = response
        });

        return Results.Ok(response);
    }

    private static void LogTicketApproved(
        ILogger<TicketHandlersLogCategory> logger,
        string ticketId,
        int approvedByUserId)
    {
        logger.LogInformation(
            "Ticket {TicketId} approved by user {UserId}.",
            ticketId,
            approvedByUserId);
    }

    public static async Task<IResult> CreateTicket(
        CreateTicketRequest request,
        ITicketRepository repo,
        IServiceScopeFactory serviceScopeFactory,
        IUserContextService userContext,
        IUserRepository userRepository,
        ISlaConfigurationService slaConfigurationService,
        ITicketStatusService ticketStatusService,
        ITicketBoardService ticketBoardService,
        ITicketRoutingRuleService ticketRoutingRuleService,
        ITicketAuditService ticketAuditService,
        [FromServices] IOperationalRiskService operationalRiskService,
        [FromServices] IReassignmentRecommendationService reassignmentRecommendationService,
        INotificationService notificationService,
        IRealtimeEventService realtimeEventService,
        IRealtimeAudienceResolver realtimeAudienceResolver,
        IResponseMappingContextFactory mappingContextFactory,
        IWorkflowMetricsService workflowMetrics,
        ICortexDecisionService? cortexDecisionService,
        ILogger<TicketHandlersLogCategory> logger,
        [FromServices] ITicketOutcomeService? ticketOutcomeService = null,
        [FromServices] ICortexAutonomyService? cortexAutonomyService = null)
    {
        _ = ticketStatusService;
        _ = notificationService;
        _ = cortexDecisionService;

        try
        {
            var createdTicketResponse = await CreateTicketCoreAsync(
                request,
                repo,
                serviceScopeFactory,
                userContext,
                userRepository,
                slaConfigurationService,
                ticketBoardService,
                ticketRoutingRuleService,
                ticketAuditService,
                operationalRiskService,
                reassignmentRecommendationService,
                realtimeEventService,
                realtimeAudienceResolver,
                mappingContextFactory,
                workflowMetrics,
                logger,
                ticketOutcomeService,
                cortexAutonomyService,
                CancellationToken.None);

            return Results.Created(
                $"/api/tickets/{createdTicketResponse.Id}",
                createdTicketResponse);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Results.Problem(exception.Message);
        }
    }

    /// <summary>
    /// Shared ticket creation path for HTTP and internal callers (e.g. integrations).
    /// </summary>
    internal static async Task<TicketResponse> CreateTicketCoreAsync(
        CreateTicketRequest request,
        ITicketRepository repo,
        IServiceScopeFactory serviceScopeFactory,
        IUserContextService userContext,
        IUserRepository userRepository,
        ISlaConfigurationService slaConfigurationService,
        ITicketBoardService ticketBoardService,
        ITicketRoutingRuleService ticketRoutingRuleService,
        ITicketAuditService ticketAuditService,
        IOperationalRiskService operationalRiskService,
        IReassignmentRecommendationService reassignmentRecommendationService,
        IRealtimeEventService realtimeEventService,
        IRealtimeAudienceResolver realtimeAudienceResolver,
        IResponseMappingContextFactory mappingContextFactory,
        IWorkflowMetricsService workflowMetrics,
        ILogger<TicketHandlersLogCategory> logger,
        ITicketOutcomeService? ticketOutcomeService,
        ICortexAutonomyService? cortexAutonomyService,
        CancellationToken cancellationToken)
    {
        ValidateCreateTicketRequest(request);

        var nextTicketId = await repo.GetNextTicketIdAsync();
        var currentUser = await userContext.GetCurrentUserAsync();
        var createStatus = "New";
        var normalizedPriority = NormalizePriority(request.Priority!);

        var selectedBoard = await ResolveBoardForCreateAsync(ticketBoardService, request.BoardId);
        var storyPoints = ResolveStoryPoints(
            selectedBoard,
            request.StoryPoints,
            null);
        var routingFactors = BuildRoutingFactors(
            boardId: selectedBoard.Id,
            priority: normalizedPriority,
            requesterDepartment: currentUser.Department,
            requesterRole: currentUser.Role,
            legacyDepartment: request.Department ?? currentUser.Department,
            legacyTitle: request.Title);
        var routingDecision = await ticketRoutingRuleService.EvaluateAsync(routingFactors);
        var manualSynitiOwner = NormalizeOptionalValue(request.SynitiOwner);
        var manualBusinessOwner = NormalizeOptionalValue(request.BusinessOwner);
        var resolvedSynitiOwner = manualSynitiOwner
            ?? routingDecision.RecommendedSynitiOwner;
        var resolvedBusinessOwner = manualBusinessOwner
            ?? routingDecision.RecommendedBusinessOwner
            ?? GetDefaultBusinessOwner(currentUser);

        var normalizedOwners = await TicketOwnerAssignmentValidation.NormalizeAndValidateAsync(
            userRepository,
            resolvedSynitiOwner,
            resolvedBusinessOwner);
        resolvedSynitiOwner = normalizedOwners.SynitiOwner;
        resolvedBusinessOwner = normalizedOwners.BusinessOwner;

        var ticket = new Ticket
        {
            Id = nextTicketId,
            Title = request.Title.Trim(),
            Description = request.Description!.Trim(),
            Priority = normalizedPriority,
            BoardId = selectedBoard.Id,
            StoryPoints = storyPoints,
            SynitiOwner = resolvedSynitiOwner,
            BusinessOwner = resolvedBusinessOwner,
            Status = createStatus,
            ApprovalStatus = ApprovalStatus.PendingApproval,
            CreatedBy = currentUser.Id,
            CreatedDate = DateTime.UtcNow
        };

        await repo.CreateTicketAsync(ticket);
        await repo.SaveChangesAsync();

        await ticketRoutingRuleService.RecordDecisionAsync(ticket.Id, routingDecision);
        if (manualSynitiOwner is not null || manualBusinessOwner is not null)
        {
            await ticketRoutingRuleService.RecordOverrideAsync(
                ticketId: ticket.Id,
                overriddenByUserId: currentUser.Id,
                previousSynitiOwner: routingDecision.RecommendedSynitiOwner,
                previousBusinessOwner: routingDecision.RecommendedBusinessOwner,
                newSynitiOwner: resolvedSynitiOwner,
                newBusinessOwner: resolvedBusinessOwner,
                reasonType: RoutingOverrideReasonType.ManualAssignment,
                reasonText: "Ticket created with manual owner selection.");
        }

        if (ticketOutcomeService is not null)
        {
            await ticketOutcomeService.RecordInitialAssignmentAsync(
                ticket,
                routingDecision.MatchedRuleId,
                cancellationToken);
            if (manualSynitiOwner is not null || manualBusinessOwner is not null)
            {
                await ticketOutcomeService.RecordOverrideAsync(
                    ticket.Id,
                    resolvedSynitiOwner,
                    resolvedBusinessOwner,
                    cancellationToken);
            }
        }

        var createdTicket = await repo.GetTicketByIdAsync(ticket.Id);

        if (createdTicket is null)
        {
            throw new InvalidOperationException("Ticket was created but could not be retrieved.");
        }

        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();
        var mappingContext = await mappingContextFactory.CreateAsync(
            [createdTicket.CreatedBy],
            null,
            [createdTicket.BoardId]);
        var createdTicketResponse = await MapTicketResponseAsync(
            createdTicket,
            slaConfigurations,
            mappingContext,
            operationalRiskService,
            reassignmentRecommendationService);
        var audienceUserIds = await realtimeAudienceResolver.GetAudienceUserIdsAsync(createdTicket);

        await ticketAuditService.RecordTicketCreatedAsync(
            createdTicket,
            currentUser,
            request.ChangeReason);
        await realtimeEventService.PublishAsync(new RealtimeEventMessage
        {
            EventType = "ticket.created",
            TicketId = createdTicket.Id,
            EntityId = createdTicket.Id,
            AudienceUserIds = audienceUserIds,
            Ticket = createdTicketResponse
        });

        LogTicketCreated(
            logger,
            createdTicket.Id,
            currentUser.Id,
            createdTicket.BoardId,
            createdTicket.Status);

        await RecordIntakeAssistSaveMetricAsync(
            workflowMetrics,
            request.IntakeAssistSave,
            createdTicket.Id,
            cancellationToken);

        QueuePostSubmitEnrichment(
            serviceScopeFactory,
            createdTicket.Id,
            runTriage: true,
            runEmbedding: true,
            logger);

        await TryRunAutonomyEvaluationAsync(cortexAutonomyService, createdTicket, logger, cancellationToken);

        return createdTicketResponse;
    }

    private static async Task TryRunAutonomyEvaluationAsync(
        ICortexAutonomyService? cortexAutonomyService,
        Ticket? ticket,
        ILogger<TicketHandlersLogCategory> logger,
        CancellationToken cancellationToken)
    {
        if (cortexAutonomyService is null || ticket is null || string.IsNullOrWhiteSpace(ticket.Id))
        {
            return;
        }

        try
        {
            await cortexAutonomyService.EvaluateAndMaybeApplyDecisionAsync(ticket, cancellationToken);
        }
        catch (Exception ex)
        {
            // Autonomy is advisory; do not surface failures to the caller.
            logger.LogWarning(
                ex,
                "Cortex autonomy evaluation failed for ticket {TicketId}; continuing without auto-evaluation.",
                ticket.Id);
        }
    }

    public static async Task<IResult> UpdateTicket(
        string id,
        UpdateTicketRequest request,
        HttpContext httpContext,
        ITicketRepository repo,
        IServiceScopeFactory serviceScopeFactory,
        IUserContextService userContext,
        IUserRepository userRepository,
        ISlaConfigurationService slaConfigurationService,
        ITicketStatusService ticketStatusService,
        ITicketBoardService ticketBoardService,
        ITicketRoutingRuleService ticketRoutingRuleService,
        ITicketAuditService ticketAuditService,
        [FromServices] IOperationalRiskService operationalRiskService,
        [FromServices] IReassignmentRecommendationService reassignmentRecommendationService,
        INotificationService notificationService,
        IRealtimeEventService realtimeEventService,
        IRealtimeAudienceResolver realtimeAudienceResolver,
        IResponseMappingContextFactory mappingContextFactory,
        IWorkflowMetricsService workflowMetrics,
        ILogger<TicketHandlersLogCategory> logger,
        [FromServices] ICortexMemoryFeedbackService? cortexMemoryFeedbackService = null,
        [FromServices] ITicketOutcomeService? ticketOutcomeService = null)
    {
        try
        {
            ValidateUpdateTicketRequest(request);

            var existing = await repo.GetTicketByIdAsync(id);
            var currentUser = await userContext.GetCurrentUserAsync();
            if (existing is null)
                return Results.NotFound();

            if (string.IsNullOrWhiteSpace(request.ConcurrencyToken))
            {
                return Results.BadRequest(new
                {
                    message =
                        "Concurrency token is required to update a ticket. Refresh the page and try again.",
                });
            }

            byte[] incomingRowVersion;
            try
            {
                incomingRowVersion = Convert.FromBase64String(request.ConcurrencyToken.Trim());
            }
            catch (FormatException)
            {
                return Results.BadRequest(new { message = "Invalid concurrency token." });
            }

            if (existing.RowVersion is not { Length: > 0 }
                || !incomingRowVersion.AsSpan().SequenceEqual(existing.RowVersion))
            {
                return Results.Conflict(new
                {
                    message =
                        "This ticket was updated elsewhere. Refresh the page to load the latest version before saving again.",
                });
            }

            if (existing.ApprovalStatus == ApprovalStatus.NeedsMoreInfo
                && existing.CreatedBy != currentUser.Id)
            {
                return Results.Json(
                    new
                    {
                        message =
                            "Only the requester can update this ticket while more information is needed.",
                    },
                    statusCode: StatusCodes.Status403Forbidden);
            }

            var hasBusinessTicketEdit = HasBusinessTicketEditRole(httpContext.User);
            var isRequesterNeedsMoreInfoRevision =
                existing.ApprovalStatus == ApprovalStatus.NeedsMoreInfo
                && existing.CreatedBy == currentUser.Id;

            if (!hasBusinessTicketEdit)
            {
                if (!isRequesterNeedsMoreInfoRevision)
                {
                    return Results.Json(
                        new { message = "You do not have permission to update this ticket." },
                        statusCode: StatusCodes.Status403Forbidden);
                }

                request = RestrictUpdateRequestForRequesterNeedsMoreInfoRevision(request);
            }
            else if (isRequesterNeedsMoreInfoRevision)
            {
                request = RestrictUpdateRequestForRequesterNeedsMoreInfoRevision(request);
            }

            var originalTicket = CloneTicket(existing);

            var resolvedTitle = request.Title is null ? existing.Title : request.Title.Trim();
            var resolvedDescription = request.Description is null ? existing.Description : request.Description.Trim();
            var resolvedPriority = request.Priority is null ? existing.Priority : NormalizePriority(request.Priority);

            var resolvedStatus = await ResolveStatusForUpdateAsync(
                ticketStatusService,
                request.Status,
                existing.Status);

            var targetBoard = await ResolveBoardForUpdateAsync(
                ticketBoardService,
                request.BoardId,
                existing.BoardId);
            var ticketRequester = await userRepository.GetByIdAsync(existing.CreatedBy);
            var requesterDepartment = ticketRequester?.Department;
            var requesterRole = ticketRequester?.Role;
            var legacyDepartment = request.Department ?? requesterDepartment;
            var routingFactorsChanged =
                targetBoard.Id != existing.BoardId
                || !string.Equals(resolvedPriority, existing.Priority, StringComparison.OrdinalIgnoreCase)
                || request.Department is not null;
            RoutingDecisionResult? routingDecision = null;
            if (routingFactorsChanged && existing.ApprovalStatus == ApprovalStatus.Approved)
            {
                routingDecision = await ticketRoutingRuleService.EvaluateAsync(
                    BuildRoutingFactors(
                        boardId: targetBoard.Id,
                        priority: resolvedPriority,
                        requesterDepartment: requesterDepartment,
                        requesterRole: requesterRole,
                        legacyDepartment: legacyDepartment,
                        legacyTitle: resolvedTitle),
                    existing.Id);
                await ticketRoutingRuleService.RecordDecisionAsync(existing.Id, routingDecision);
            }

            var storyPoints = ResolveStoryPoints(
                targetBoard,
                request.StoryPoints,
                existing.StoryPoints);

            existing.Title = resolvedTitle;
            existing.Description = resolvedDescription;
            existing.Status = resolvedStatus;
            existing.Priority = resolvedPriority;
            existing.BoardId = targetBoard.Id;
            existing.StoryPoints = storyPoints;
            var resolvedSynitiOwner = request.SynitiOwner ?? existing.SynitiOwner;
            var resolvedBusinessOwner = request.BusinessOwner ?? existing.BusinessOwner;
            if (routingDecision is not null
                && request.SynitiOwner is null
                && request.BusinessOwner is null)
            {
                resolvedSynitiOwner = existing.SynitiOwner;
                resolvedBusinessOwner = existing.BusinessOwner;
            }

            var ownerOverrideDetected = routingDecision is not null
                && HasOwnerOverride(
                    routingDecision.RecommendedSynitiOwner,
                    routingDecision.RecommendedBusinessOwner,
                    resolvedSynitiOwner,
                    resolvedBusinessOwner);
            if (ownerOverrideDetected)
            {
                await ticketRoutingRuleService.RecordOverrideAsync(
                    ticketId: existing.Id,
                    overriddenByUserId: currentUser.Id,
                    previousSynitiOwner: routingDecision!.RecommendedSynitiOwner,
                    previousBusinessOwner: routingDecision!.RecommendedBusinessOwner,
                    newSynitiOwner: resolvedSynitiOwner,
                    newBusinessOwner: resolvedBusinessOwner,
                    reasonType: ParseOverrideReasonType(request.ChangeReason),
                    reasonText: request.ChangeReason);
            }

            var priorityOverrideDetected = request.Priority is not null
                && !string.IsNullOrWhiteSpace(originalTicket.AiTriageSuggestedPriority)
                && !string.Equals(resolvedPriority, originalTicket.AiTriageSuggestedPriority, StringComparison.OrdinalIgnoreCase);
            var statusOverrideDetected = request.Status is not null
                && !string.IsNullOrWhiteSpace(originalTicket.AiTriageSuggestedStatus)
                && !string.Equals(resolvedStatus, originalTicket.AiTriageSuggestedStatus, StringComparison.OrdinalIgnoreCase);

            var normalizedOwners = await TicketOwnerAssignmentValidation.NormalizeAndValidateAsync(
                userRepository,
                resolvedSynitiOwner,
                resolvedBusinessOwner);
            resolvedSynitiOwner = normalizedOwners.SynitiOwner;
            resolvedBusinessOwner = normalizedOwners.BusinessOwner;

            existing.SynitiOwner = resolvedSynitiOwner;
            existing.BusinessOwner = resolvedBusinessOwner;

            if (isRequesterNeedsMoreInfoRevision)
            {
                existing.ApprovalStatus = ApprovalStatus.PendingApproval;
                existing.ReturnedForDetailAt = null;
                existing.ReturnedForDetailBy = null;
                existing.ReturnReason = null;
            }

            existing.LastModifiedBy = currentUser.Id;
            existing.LastModifiedDate = DateTime.UtcNow;

            await repo.UpdateTicketAsync(existing);
            try
            {
                await repo.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                return Results.Conflict(new
                {
                    message =
                        "This ticket was updated elsewhere. Refresh the page to load the latest version before saving again.",
                });
            }

            if (ownerOverrideDetected)
                await TryRecordFeedbackAsync(cortexMemoryFeedbackService, currentUser, existing.Id, CortexMemoryEventType.OwnerOverridden, "TicketUpdate", null, CancellationToken.None);
            if (priorityOverrideDetected)
                await TryRecordFeedbackAsync(cortexMemoryFeedbackService, currentUser, existing.Id, CortexMemoryEventType.PriorityOverridden, "TicketUpdate", null, CancellationToken.None);
            if (statusOverrideDetected)
                await TryRecordFeedbackAsync(cortexMemoryFeedbackService, currentUser, existing.Id, CortexMemoryEventType.StatusOverridden, "TicketUpdate", null, CancellationToken.None);

            var updatedTicket = await repo.GetTicketByIdAsync(id);

            if (updatedTicket is null)
                return Results.Problem("Ticket was updated but could not be retrieved.");

            if (ticketOutcomeService is not null)
            {
                if (ownerOverrideDetected)
                {
                    await ticketOutcomeService.RecordOverrideAsync(
                        updatedTicket.Id,
                        updatedTicket.SynitiOwner,
                        updatedTicket.BusinessOwner,
                        CancellationToken.None);
                }
                if (HasMeaningfulOwnerChange(originalTicket.SynitiOwner, updatedTicket.SynitiOwner))
                {
                    await ticketOutcomeService.MarkReassignedAsync(
                        updatedTicket,
                        originalTicket.SynitiOwner,
                        CancellationToken.None);
                }

                var wasTerminalBefore = TicketOutcomeService.IsTerminalStatus(originalTicket.Status);
                var isTerminalNow = TicketOutcomeService.IsTerminalStatus(updatedTicket.Status);
                if (!wasTerminalBefore && isTerminalNow)
                {
                    await ticketOutcomeService.RecordTerminalAsync(
                        updatedTicket,
                        CancellationToken.None);
                }
                else if (wasTerminalBefore && !isTerminalNow)
                {
                    await ticketOutcomeService.RecordReopenAsync(
                        updatedTicket.Id,
                        CancellationToken.None);
                }
            }

            var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();
            var mappingContext = await mappingContextFactory.CreateAsync(
                [updatedTicket.CreatedBy],
                null,
                [updatedTicket.BoardId]);
            var updatedTicketResponse = await MapTicketResponseAsync(
                updatedTicket,
                slaConfigurations,
                mappingContext,
                operationalRiskService,
                reassignmentRecommendationService);
            var originalAudienceUserIds = await realtimeAudienceResolver.GetAudienceUserIdsAsync(originalTicket);
            var updatedAudienceUserIds = await realtimeAudienceResolver.GetAudienceUserIdsAsync(updatedTicket);
            var removedAudienceUserIds = originalAudienceUserIds
                .Except(updatedAudienceUserIds)
                .ToArray();

            await ticketAuditService.RecordTicketUpdatedAsync(
                originalTicket,
                updatedTicket,
                currentUser,
                request.ChangeReason);
            await notificationService.CreateAssignmentNotificationsAsync(
                originalTicket,
                updatedTicket,
                currentUser);
            if (removedAudienceUserIds.Length > 0)
            {
                await realtimeEventService.PublishAsync(new RealtimeEventMessage
                {
                    EventType = "ticket.removed",
                    TicketId = updatedTicket.Id,
                    EntityId = updatedTicket.Id,
                    AudienceUserIds = removedAudienceUserIds
                });
            }

            await realtimeEventService.PublishAsync(new RealtimeEventMessage
            {
                EventType = "ticket.updated",
                TicketId = updatedTicket.Id,
                EntityId = updatedTicket.Id,
                AudienceUserIds = updatedAudienceUserIds,
                Ticket = updatedTicketResponse
            });
            if (routingFactorsChanged)
            {
                await realtimeEventService.PublishAsync(new RealtimeEventMessage
                {
                    EventType = "ticket.routed",
                    TicketId = updatedTicket.Id,
                    EntityId = updatedTicket.Id,
                    AudienceUserIds = updatedAudienceUserIds,
                    Ticket = updatedTicketResponse
                });
            }

            LogTicketUpdatedLifecycle(
                logger,
                originalTicket,
                updatedTicket,
                currentUser.Id);

            await RecordIntakeAssistSaveMetricAsync(
                workflowMetrics,
                request.IntakeAssistSave,
                updatedTicket.Id,
                CancellationToken.None);

            QueuePostSubmitEnrichment(
                serviceScopeFactory,
                updatedTicket.Id,
                runTriage: isRequesterNeedsMoreInfoRevision,
                runEmbedding: true,
                logger);

            return Results.Ok(updatedTicketResponse);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
        catch (KeyNotFoundException exception)
        {
            return Results.BadRequest(new { message = exception.Message });
        }
    }

    private static async Task RecordIntakeAssistSaveMetricAsync(
        IWorkflowMetricsService metrics,
        IntakeAssistSaveMetrics? save,
        string ticketId,
        CancellationToken cancellationToken)
    {
        if (save?.IntakeAssistUsedBeforeSave != true)
        {
            return;
        }

        await metrics.TryRecordAsync(
            "intake_assist_saved",
            new
            {
                intakeAssistUsedBeforeSave = true,
                clarityState = save.LastIntakeClarityState,
                missingDetailCount = save.LastIntakeMissingDetailCount,
            },
            ticketId,
            cancellationToken);
    }

    private static void QueuePostSubmitEnrichment(
        IServiceScopeFactory serviceScopeFactory,
        string ticketId,
        bool runTriage,
        bool runEmbedding,
        ILogger<TicketHandlersLogCategory> logger)
    {
        if (string.IsNullOrWhiteSpace(ticketId) || (!runTriage && !runEmbedding))
        {
            return;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = serviceScopeFactory.CreateScope();
                var scopedRepo = scope.ServiceProvider.GetRequiredService<ITicketRepository>();
                var ticket = await scopedRepo.GetTicketByIdAsync(ticketId);
                if (ticket is null)
                {
                    logger.LogWarning(
                        "Post-submit enrichment skipped because ticket {TicketId} could not be loaded.",
                        ticketId);
                    return;
                }

                if (runTriage)
                {
                    var triageAi = scope.ServiceProvider.GetRequiredService<ITicketTriageAiService>();
                    var triageVocabulary = scope.ServiceProvider.GetRequiredService<ITicketTriageVocabularyProvider>();
                    var userRepository = scope.ServiceProvider.GetRequiredService<IUserRepository>();
                    var ticketBoardService = scope.ServiceProvider.GetRequiredService<ITicketBoardService>();
                    var aiSettingsService = scope.ServiceProvider.GetRequiredService<IAiSettingsService>();
                    var aiSettings = await aiSettingsService.GetAsync();

                    await TicketTriagePersistence.TryGenerateAndPersistAsync(
                        ticket,
                        scopedRepo,
                        triageAi,
                        triageVocabulary,
                        userRepository,
                        ticketBoardService,
                        aiSettings,
                        logger,
                        CancellationToken.None);
                }

                if (runEmbedding)
                {
                    var cortexEmbeddingService = scope.ServiceProvider.GetService<ICortexEmbeddingService>();
                    await TryEnsureEmbeddingAsync(
                        cortexEmbeddingService,
                        ticketId,
                        logger,
                        CancellationToken.None);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Post-submit enrichment failed for ticket {TicketId}. Ticket save already completed.",
                    ticketId);
            }
        });
    }

    public static async Task<IResult> ArchiveTicket(
        string id,
        TicketActionReasonRequest? request,
        ITicketRepository repo,
        IUserContextService userContext,
        ITicketVisibilityService ticketVisibilityService,
        ITicketAuditService ticketAuditService,
        INotificationService notificationService,
        IRealtimeEventService realtimeEventService,
        IRealtimeAudienceResolver realtimeAudienceResolver,
        IResponseMappingContextFactory mappingContextFactory)
    {
        var existing = await repo.GetTicketByIdAsync(id);
        if (existing is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(existing))
        {
            return Results.NotFound();
        }

        var currentUser = await userContext.GetCurrentUserAsync();
        var archived = await repo.ArchiveTicketAsync(id, currentUser.Id);
        if (!archived)
        {
            return Results.Problem("Ticket could not be archived.");
        }

        var archivedTicket = await repo.GetArchivedTicketByIdAsync(id);
        if (archivedTicket is null)
        {
            return Results.Problem("Ticket was archived but could not be retrieved.");
        }

        var mappingContext = await mappingContextFactory.CreateAsync(
            [archivedTicket.CreatedBy, archivedTicket.ArchivedBy],
            null,
            [archivedTicket.BoardId]);
        var archivedTicketResponse = archivedTicket.ToResponse(mappingContext);
        var audienceUserIds = await realtimeAudienceResolver.GetAudienceUserIdsAsync(archivedTicket);

        await ticketAuditService.RecordTicketArchivedAsync(
            existing,
            currentUser,
            request?.ChangeReason);
        await notificationService.CreateArchiveNotificationsAsync(
            existing,
            currentUser,
            ticketIsArchived: true);
        await realtimeEventService.PublishAsync(new RealtimeEventMessage
        {
            EventType = "ticket.archived",
            TicketId = existing.Id,
            EntityId = existing.Id,
            AudienceUserIds = audienceUserIds,
            ArchivedTicket = archivedTicketResponse
        });

        return Results.Ok(archivedTicketResponse);
    }

    public static async Task<IResult> ReactivateArchivedTicket(
        string id,
        ITicketRepository repo,
        IUserContextService userContext,
        ITicketVisibilityService ticketVisibilityService,
        ISlaConfigurationService slaConfigurationService,
        ITicketStatusService ticketStatusService,
        ITicketAuditService ticketAuditService,
        [FromServices] IOperationalRiskService operationalRiskService,
        [FromServices] IReassignmentRecommendationService reassignmentRecommendationService,
        INotificationService notificationService,
        IRealtimeEventService realtimeEventService,
        IRealtimeAudienceResolver realtimeAudienceResolver,
        IResponseMappingContextFactory mappingContextFactory)
    {
        var archivedTicket = await repo.GetArchivedTicketByIdAsync(id);
        if (archivedTicket is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(
                archivedTicket.CreatedBy,
                archivedTicket.SynitiOwner,
                archivedTicket.BusinessOwner))
        {
            return Results.NotFound();
        }

        var activeTicket = await repo.GetTicketByIdAsync(id);
        if (activeTicket is not null)
        {
            return Results.Conflict(new { message = "An active ticket with this ID already exists." });
        }

        var currentUser = await userContext.GetCurrentUserAsync();
        var restoredStatus = await ticketStatusService.GetReactivatedStatusAsync(archivedTicket.Status);
        var reactivated = await repo.ReactivateArchivedTicketAsync(id, currentUser.Id, restoredStatus);
        if (!reactivated)
        {
            return Results.Problem("Ticket could not be reactivated.");
        }

        var restoredTicket = await repo.GetTicketByIdAsync(id);
        if (restoredTicket is null)
        {
            return Results.Problem("Ticket was reactivated but could not be retrieved.");
        }

        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();
        var mappingContext = await mappingContextFactory.CreateAsync(
            [restoredTicket.CreatedBy],
            null,
            [restoredTicket.BoardId]);
        var restoredTicketResponse = await MapTicketResponseAsync(
            restoredTicket,
            slaConfigurations,
            mappingContext,
            operationalRiskService,
            reassignmentRecommendationService);
        var audienceUserIds = await realtimeAudienceResolver.GetAudienceUserIdsAsync(restoredTicket);

        await ticketAuditService.RecordTicketReactivatedAsync(
            archivedTicket,
            restoredTicket,
            currentUser,
            null);
        await notificationService.CreateArchiveNotificationsAsync(
            restoredTicket,
            currentUser,
            ticketIsArchived: false,
            isReactivated: true);
        await realtimeEventService.PublishAsync(new RealtimeEventMessage
        {
            EventType = "ticket.reactivated",
            TicketId = restoredTicket.Id,
            EntityId = restoredTicket.Id,
            AudienceUserIds = audienceUserIds,
            Ticket = restoredTicketResponse
        });

        return Results.Ok(restoredTicketResponse);
    }

    public static async Task<IResult> DeleteTicket(
        string id,
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        IRealtimeEventService realtimeEventService,
        IRealtimeAudienceResolver realtimeAudienceResolver)
    {
        var normalizedId = id.Trim();
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return Results.BadRequest("Invalid ticket id.");
        }

        var existing = await repo.GetTicketByIdAsync(normalizedId);
        if (existing is null)
        {
            return Results.NotFound();
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(existing))
        {
            return Results.NotFound();
        }

        var audienceUserIds = await realtimeAudienceResolver.GetAudienceUserIdsAsync(existing);

        var deleted = await repo.DeleteTicketAsync(normalizedId);

        if (!deleted)
        {
            return Results.NotFound();
        }

        await repo.SaveChangesAsync();

        await realtimeEventService.PublishAsync(new RealtimeEventMessage
        {
            EventType = "ticket.deleted",
            TicketId = existing.Id,
            EntityId = existing.Id,
            AudienceUserIds = audienceUserIds,
        });

        return Results.NoContent();
    }

    private static void LogTicketCreated(
        ILogger logger,
        string ticketId,
        int actingUserId,
        int boardId,
        string status)
    {
        logger.LogInformation(
            "Ticket created. TicketId={TicketId} ActingUserId={ActingUserId} BoardId={BoardId} Status={Status}",
            ticketId,
            actingUserId,
            boardId,
            status);
    }

    private static void LogTicketUpdatedLifecycle(
        ILogger logger,
        Ticket original,
        Ticket updated,
        int actingUserId)
    {
        var statusChanged = !string.Equals(original.Status, updated.Status, StringComparison.Ordinal);
        var synitiOwnerChanged = !OwnerFieldsEqual(original.SynitiOwner, updated.SynitiOwner);
        var businessOwnerChanged = !OwnerFieldsEqual(original.BusinessOwner, updated.BusinessOwner);

        logger.LogInformation(
            "Ticket updated. TicketId={TicketId} ActingUserId={ActingUserId} BoardId={BoardId} " +
            "StatusChanged={StatusChanged} PreviousStatus={PreviousStatus} NewStatus={NewStatus} " +
            "SynitiOwnerChanged={SynitiOwnerChanged} BusinessOwnerChanged={BusinessOwnerChanged}",
            updated.Id,
            actingUserId,
            updated.BoardId,
            statusChanged,
            original.Status,
            updated.Status,
            synitiOwnerChanged,
            businessOwnerChanged);
    }

    private static bool OwnerFieldsEqual(string? left, string? right)
    {
        return string.Equals(
            NormalizeOptionalValue(left) ?? string.Empty,
            NormalizeOptionalValue(right) ?? string.Empty,
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasMeaningfulOwnerChange(string? previousOwner, string? currentOwner)
    {
        var previous = NormalizeOptionalValue(previousOwner);
        var current = NormalizeOptionalValue(currentOwner);

        return previous is not null
            && current is not null
            && !string.Equals(previous, current, StringComparison.OrdinalIgnoreCase);
    }

    private static Ticket CloneTicket(Ticket ticket)
    {
        return new Ticket
        {
            Id = ticket.Id,
            Title = ticket.Title,
            Description = ticket.Description,
            Status = ticket.Status,
            ApprovalStatus = ticket.ApprovalStatus,
            Priority = ticket.Priority,
            BoardId = ticket.BoardId,
            StoryPoints = ticket.StoryPoints,
            SynitiOwner = ticket.SynitiOwner,
            BusinessOwner = ticket.BusinessOwner,
            CreatedBy = ticket.CreatedBy,
            CreatedDate = ticket.CreatedDate,
            LastModifiedBy = ticket.LastModifiedBy,
            LastModifiedDate = ticket.LastModifiedDate,
            ApprovedAt = ticket.ApprovedAt,
            ApprovedBy = ticket.ApprovedBy,
            RejectedAt = ticket.RejectedAt,
            RejectedBy = ticket.RejectedBy,
            RejectionReason = ticket.RejectionReason,
            ReturnedForDetailAt = ticket.ReturnedForDetailAt,
            ReturnedForDetailBy = ticket.ReturnedForDetailBy,
            ReturnReason = ticket.ReturnReason,
            RowVersion = ticket.RowVersion is { Length: > 0 }
                ? (byte[])ticket.RowVersion.Clone()
                : [],
        };
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static string? TryExtractRecommendedOwnerFromDecisionExplanation(
        string? explanationJson,
        string slotKey)
    {
        if (string.IsNullOrWhiteSpace(explanationJson) || string.IsNullOrWhiteSpace(slotKey))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(explanationJson);
            if (!document.RootElement.TryGetProperty("slots", out var slots))
            {
                return null;
            }

            if (!slots.TryGetProperty(slotKey, out var slot))
            {
                return null;
            }

            if (!slot.TryGetProperty("selectedOwnerKey", out var selectedOwner))
            {
                return null;
            }

            return NormalizeOptionalValue(selectedOwner.GetString());
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static RoutingFactors BuildRoutingFactors(
        int boardId,
        string priority,
        string? requesterDepartment,
        string? requesterRole,
        string? legacyDepartment,
        string? legacyTitle)
    {
        return new RoutingFactors(
            BoardId: boardId.ToString(CultureInfo.InvariantCulture),
            Priority: NormalizeOptionalValue(priority),
            RequesterDepartment: NormalizeOptionalValue(requesterDepartment),
            RequesterRole: NormalizeOptionalValue(requesterRole),
            LegacyDepartment: NormalizeOptionalValue(legacyDepartment),
            LegacyTitle: NormalizeOptionalValue(legacyTitle));
    }

    private static bool HasOwnerOverride(
        string? recommendedSynitiOwner,
        string? recommendedBusinessOwner,
        string? chosenSynitiOwner,
        string? chosenBusinessOwner)
    {
        return !OwnerFieldsEqual(recommendedSynitiOwner, chosenSynitiOwner)
            || !OwnerFieldsEqual(recommendedBusinessOwner, chosenBusinessOwner);
    }

    private static RoutingOverrideReasonType ParseOverrideReasonType(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return RoutingOverrideReasonType.ManualAssignment;
        }

        var normalized = reason.Trim().ToLowerInvariant();
        if (normalized.Contains("workload", StringComparison.Ordinal))
        {
            return RoutingOverrideReasonType.WorkloadAdjustment;
        }

        if (normalized.Contains("escalat", StringComparison.Ordinal))
        {
            return RoutingOverrideReasonType.Escalation;
        }

        if (normalized.Contains("incorrect", StringComparison.Ordinal)
            || normalized.Contains("wrong", StringComparison.Ordinal))
        {
            return RoutingOverrideReasonType.IncorrectRouting;
        }

        return RoutingOverrideReasonType.Other;
    }

    private static void ValidateCreateTicketRequest(CreateTicketRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            throw new ArgumentException("Title is required.");
        }

        if (request.Title.Trim().Length > MaxTitleLength)
        {
            throw new ArgumentException($"Title must be {MaxTitleLength} characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(request.Description))
        {
            throw new ArgumentException("Description is required.");
        }

        if (request.Description.Trim().Length > MaxDescriptionLength)
        {
            throw new ArgumentException($"Description must be {MaxDescriptionLength} characters or fewer.");
        }

        if (string.IsNullOrWhiteSpace(request.Priority))
        {
            throw new ArgumentException("Priority is required.");
        }

        if (!string.IsNullOrWhiteSpace(request.Status))
        {
            throw new ArgumentException("Status cannot be provided when creating a ticket.");
        }
    }

    private static void ValidateUpdateTicketRequest(UpdateTicketRequest request)
    {
        if (request.Title is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                throw new ArgumentException("Title cannot be empty.");
            }

            if (request.Title.Trim().Length > MaxTitleLength)
            {
                throw new ArgumentException($"Title must be {MaxTitleLength} characters or fewer.");
            }
        }

        if (request.Description is not null)
        {
            if (string.IsNullOrWhiteSpace(request.Description))
            {
                throw new ArgumentException("Description cannot be empty.");
            }

            if (request.Description.Trim().Length > MaxDescriptionLength)
            {
                throw new ArgumentException($"Description must be {MaxDescriptionLength} characters or fewer.");
            }
        }

        if (request.Priority is not null && string.IsNullOrWhiteSpace(request.Priority))
        {
            throw new ArgumentException("Priority cannot be empty.");
        }

        if (request.Status is not null && string.IsNullOrWhiteSpace(request.Status))
        {
            throw new ArgumentException("Status cannot be empty.");
        }
    }

    private static string NormalizePriority(string priority)
    {
        var normalizedPriority = AllowedPriorities.FirstOrDefault(candidate =>
            string.Equals(candidate, priority.Trim(), StringComparison.OrdinalIgnoreCase));
        if (normalizedPriority is null)
        {
            throw new ArgumentException(
                $"Priority must be one of: {string.Join(", ", AllowedPriorities)}.");
        }

        return normalizedPriority;
    }

    private static string ResolveApprovedPriority(string currentPriority, string? triageSuggestedPriority)
    {
        if (!string.IsNullOrWhiteSpace(triageSuggestedPriority))
        {
            try
            {
                return NormalizePriority(triageSuggestedPriority);
            }
            catch (ArgumentException)
            {
                // Ignore unsupported AI suggestion and retain the persisted priority.
            }
        }

        return NormalizePriority(currentPriority);
    }

    private static string? GetDefaultBusinessOwner(User user)
    {
        return OwnerRoleAssignmentRules.IsValidBusinessOwnerAssignment(user)
            ? OwnerFieldResolution.ToCanonicalOwnerKey(user)
            : null;
    }

    private static int? ResolveStoryPoints(
        TicketBoardDefinition board,
        int? requestedStoryPoints,
        int? existingStoryPoints)
    {
        if (!board.RequiresStoryPoints)
        {
            return null;
        }

        var resolvedStoryPoints = requestedStoryPoints ?? existingStoryPoints;
        if (!resolvedStoryPoints.HasValue)
        {
            throw new ArgumentException(
                $"Board \"{board.Name}\" requires story points from 1 to 5.");
        }

        if (resolvedStoryPoints.Value is < 1 or > 5)
        {
            throw new ArgumentException("Story points must be between 1 and 5.");
        }

        return resolvedStoryPoints.Value;
    }

    private static async Task<TicketBoardDefinition> ResolveBoardForCreateAsync(
        ITicketBoardService ticketBoardService,
        int? boardId)
    {
        if (!boardId.HasValue)
        {
            return await ticketBoardService.GetDefaultCreateBoardAsync();
        }

        var board = await ticketBoardService.GetByIdAsync(boardId.Value);
        if (board is null || !board.IsEnabled)
        {
            return await ticketBoardService.GetDefaultCreateBoardAsync();
        }

        return board;
    }

    private static async Task<TicketBoardDefinition> ResolveBoardForUpdateAsync(
        ITicketBoardService ticketBoardService,
        int? boardId,
        int existingBoardId)
    {
        if (!boardId.HasValue)
        {
            return await GetExistingBoardAsync(ticketBoardService, existingBoardId);
        }

        var board = await ticketBoardService.GetByIdAsync(boardId.Value);
        if (board is null)
        {
            throw new ArgumentException("BoardId must reference a configured ticket board.");
        }

        if (!board.IsEnabled && board.Id != existingBoardId)
        {
            throw new ArgumentException("BoardId must reference an enabled ticket board.");
        }

        return board;
    }

    private static async Task<string> ResolveStatusForUpdateAsync(
        ITicketStatusService ticketStatusService,
        string? requestedStatus,
        string existingStatus)
    {
        if (requestedStatus is null)
        {
            return existingStatus;
        }

        var trimmedStatus = requestedStatus.Trim();
        if (string.Equals(trimmedStatus, existingStatus, StringComparison.OrdinalIgnoreCase))
        {
            return existingStatus;
        }

        await ticketStatusService.EnsureSelectableStatusAsync(trimmedStatus);

        var enabledStatuses = await ticketStatusService.GetEnabledAsync();
        var matchedStatus = enabledStatuses.FirstOrDefault(definition =>
            string.Equals(definition.Name, trimmedStatus, StringComparison.OrdinalIgnoreCase));

        return matchedStatus?.Name ?? trimmedStatus;
    }

    private static async Task<TicketBoardDefinition> GetExistingBoardAsync(
        ITicketBoardService ticketBoardService,
        int boardId)
    {
        return await ticketBoardService.GetByIdAsync(boardId)
            ?? await ticketBoardService.GetDefaultCreateBoardAsync();
    }

    private static bool HasBusinessTicketEditRole(System.Security.Claims.ClaimsPrincipal user) =>
        user.IsInRole(Auth0Roles.Admin) ||
        user.IsInRole(Auth0Roles.Developer) ||
        user.IsInRole(Auth0Roles.BusinessManager);

    /// <summary>
    /// Explicit reviewer action that applies persisted AI triage suggestions to canonical fields.
    /// No AI call; re-validates each selected suggestion against the live vocabulary before mutating.
    /// </summary>
    public static async Task<IResult> ApplyTicketTriageSuggestions(
        string id,
        TicketTriageApplyRequest request,
        ITicketRepository repo,
        IUserContextService userContext,
        ITicketVisibilityService visibilityService,
        ITicketTriageVocabularyProvider triageVocabulary,
        ITicketAuditService ticketAuditService,
        ISlaConfigurationService slaConfigurationService,
        IResponseMappingContextFactory mappingContextFactory,
        [FromServices] IOperationalRiskService operationalRiskService,
        [FromServices] IReassignmentRecommendationService reassignmentRecommendationService,
        IRealtimeEventService realtimeEventService,
        IRealtimeAudienceResolver realtimeAudienceResolver,
        ILogger<TicketHandlersLogCategory> logger,
        CancellationToken cancellationToken = default,
        [FromServices] ICortexEmbeddingService? cortexEmbeddingService = null,
        [FromServices] ICortexMemoryFeedbackService? cortexMemoryFeedbackService = null,
        [FromServices] ITicketOutcomeService? ticketOutcomeService = null)
    {
        var ticket = await repo.GetTicketByIdAsync(id.Trim());
        if (ticket is null)
        {
            return Results.NotFound();
        }

        var visibility = await visibilityService.GetCurrentVisibilityAsync();
        if (!visibility.CanView(ticket))
        {
            return Results.NotFound();
        }

        if (!request.ApplyPriority && !request.ApplyStatus)
        {
            return Results.BadRequest(new
            {
                message = "Select at least one AI triage suggestion to apply.",
            });
        }

        var changeReason = string.IsNullOrWhiteSpace(request.ChangeReason)
            ? null
            : request.ChangeReason.Trim();
        if (changeReason is not null && changeReason.Length > MaxApprovalReasonLength)
        {
            return Results.BadRequest(new
            {
                message = $"Change reason must be {MaxApprovalReasonLength} characters or fewer.",
            });
        }

        var vocabulary = await triageVocabulary.GetAsync(cancellationToken);

        string? priorityToApply = null;
        if (request.ApplyPriority)
        {
            priorityToApply = MatchVocabularyOption(
                ticket.AiTriageSuggestedPriority,
                vocabulary.Priorities.Select(p => p.Name));
            if (priorityToApply is null)
            {
                return Results.Conflict(new
                {
                    message =
                        "The AI-suggested priority is no longer a valid option. Regenerate triage and try again.",
                });
            }
        }

        string? statusToApply = null;
        if (request.ApplyStatus)
        {
            statusToApply = MatchVocabularyOption(
                ticket.AiTriageSuggestedStatus,
                vocabulary.Statuses.Select(s => s.Name));
            if (statusToApply is null)
            {
                return Results.Conflict(new
                {
                    message =
                        "The AI-suggested status is no longer a valid option. Regenerate triage and try again.",
                });
            }
        }

        var currentUser = await userContext.GetCurrentUserAsync();
        var originalSnapshot = CloneTicket(ticket);

        if (priorityToApply is not null)
        {
            ticket.Priority = priorityToApply;
        }

        if (statusToApply is not null)
        {
            ticket.Status = statusToApply;
        }

        ticket.LastModifiedBy = currentUser.Id;
        ticket.LastModifiedDate = DateTime.UtcNow;

        await repo.UpdateTicketAsync(ticket);
        try
        {
            await repo.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.Conflict(new
            {
                message =
                    "This ticket was updated elsewhere. Refresh the page to load the latest version before trying again.",
            });
        }

        if (ticketOutcomeService is not null)
        {
            var wasTerminalBefore = TicketOutcomeService.IsTerminalStatus(originalSnapshot.Status);
            var isTerminalNow = TicketOutcomeService.IsTerminalStatus(ticket.Status);
            if (!wasTerminalBefore && isTerminalNow)
            {
                await ticketOutcomeService.RecordTerminalAsync(ticket, cancellationToken);
            }
            else if (wasTerminalBefore && !isTerminalNow)
            {
                await ticketOutcomeService.RecordReopenAsync(ticket.Id, cancellationToken);
            }
        }

        await TryEnsureEmbeddingAsync(
            cortexEmbeddingService,
            ticket.Id,
            logger,
            CancellationToken.None);

        await TryRecordFeedbackAsync(
            cortexMemoryFeedbackService,
            currentUser,
            ticket.Id,
            CortexMemoryEventType.AiSuggestionAccepted,
            "TicketTriage",
            BuildAppliedTriageMetadata(priorityToApply, statusToApply),
            cancellationToken);

        await ticketAuditService.RecordTicketUpdatedAsync(
            originalSnapshot,
            ticket,
            currentUser,
            changeReason);

        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();
        var mappingContext = await mappingContextFactory.CreateAsync(
            [ticket.CreatedBy],
            null,
            [ticket.BoardId],
            cancellationToken);
        var response = await MapTicketResponseAsync(
            ticket,
            slaConfigurations,
            mappingContext,
            operationalRiskService,
            reassignmentRecommendationService,
            cancellationToken: cancellationToken);

        var audienceUserIds = await realtimeAudienceResolver.GetAudienceUserIdsAsync(
            ticket,
            cancellationToken);
        await realtimeEventService.PublishAsync(
            new RealtimeEventMessage
            {
                EventType = "ticket.updated",
                TicketId = ticket.Id,
                EntityId = ticket.Id,
                AudienceUserIds = audienceUserIds,
                Ticket = response,
            },
            cancellationToken);

        return Results.Ok(response);
    }

    public static async Task<IResult> PostMemoryFeedback(
        string ticketId,
        CortexMemoryFeedbackRequest request,
        IUserContextService userContext,
        ICortexMemoryFeedbackService feedbackService,
        CancellationToken cancellationToken = default)
    {
        if (!CortexMemoryEventType.IsValid(request.EventType))
        {
            return Results.BadRequest((object)new
            {
                message = $"Unsupported event type '{request.EventType}'. " +
                          "Use one of: RelatedTicketShown, RelatedTicketClicked, AiSuggestionAccepted, " +
                          "OwnerOverridden, PriorityOverridden, StatusOverridden.",
            });
        }

        var currentUser = await userContext.GetCurrentUserAsync();
        await feedbackService.RecordAsync(
            ticketId: ticketId.Trim(),
            eventType: request.EventType.Trim(),
            source: string.IsNullOrWhiteSpace(request.Source) ? "Frontend" : request.Source.Trim(),
            relatedTicketId: request.RelatedTicketId,
            createdByUserId: currentUser.Id,
            createdByDisplayName: currentUser.DisplayName,
            metadataJson: request.Metadata,
            cancellationToken: cancellationToken);

        return Results.NoContent();
    }

    private static async Task TryRecordFeedbackAsync(
        ICortexMemoryFeedbackService? feedbackService,
        User currentUser,
        string ticketId,
        string eventType,
        string source,
        string? metadataJson,
        CancellationToken cancellationToken = default)
    {
        if (feedbackService is null)
            return;
        await feedbackService.RecordAsync(
            ticketId: ticketId,
            eventType: eventType,
            source: source,
            createdByUserId: currentUser.Id,
            createdByDisplayName: currentUser.DisplayName,
            metadataJson: metadataJson,
            cancellationToken: cancellationToken);
    }

    private static string BuildAppliedTriageMetadata(string? priority, string? status)
    {
        var p = priority is not null ? $"\"{priority}\"" : "null";
        var s = status is not null ? $"\"{status}\"" : "null";
        return $"{{\"appliedPriority\":{p},\"appliedStatus\":{s}}}";
    }

    private static string? MatchVocabularyOption(string? candidate, IEnumerable<string> allowed)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        var trimmed = candidate.Trim();
        foreach (var name in allowed)
        {
            if (string.Equals(trimmed, name, StringComparison.OrdinalIgnoreCase))
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>Phase 1 advisory AI triage for intake review; persists on success (e.g. manual regenerate).</summary>
    public static async Task<IResult> GenerateTicketTriage(
        string id,
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        ITicketBoardService ticketBoardService,
        IUserRepository userRepository,
        IAiSettingsService aiSettingsService,
        ITicketTriageAiService triageAi,
        ITicketTriageVocabularyProvider triageVocabulary,
        ILogger<TicketHandlersLogCategory> logger,
        CancellationToken cancellationToken,
        [FromServices] ICortexEmbeddingService? cortexEmbeddingService = null)
    {
        var ticket = await repo.GetTicketByIdAsync(id.Trim());
        if (ticket is null)
        {
            return Results.NotFound();
        }

        if (ticket.ApprovalStatus != ApprovalStatus.PendingApproval)
        {
            return Results.Conflict(new
            {
                message = "AI triage is only available for tickets pending approval.",
            });
        }

        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        if (!visibilityContext.CanView(ticket))
        {
            return Results.NotFound();
        }

        var board = await ticketBoardService.GetByIdAsync(ticket.BoardId);
        var boardName = board?.Name ?? $"Board #{ticket.BoardId}";

        var requester = await userRepository.GetByIdAsync(ticket.CreatedBy);
        var aiSettings = await aiSettingsService.GetAsync();
        var vocabulary = await triageVocabulary.GetAsync(cancellationToken);

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
        if (!result.Unavailable)
        {
            TicketTriagePersistence.ApplyPersistedResult(
                ticket,
                result,
                vocabulary,
                aiSettings,
                logger);
            await repo.UpdateTicketAsync(ticket);
            await repo.SaveChangesAsync();

            await TryEnsureEmbeddingAsync(
                cortexEmbeddingService,
                ticket.Id,
                logger,
                CancellationToken.None);
        }

        return Results.Ok(result);
    }

    /// <summary>
    /// Requester revising a returned ticket may update intake fields; ownership and workflow status stay with reviewers.
    /// </summary>
    private static UpdateTicketRequest RestrictUpdateRequestForRequesterNeedsMoreInfoRevision(
        UpdateTicketRequest source) =>
        new()
        {
            ConcurrencyToken = source.ConcurrencyToken,
            Title = source.Title,
            Description = source.Description,
            Department = source.Department,
            BoardId = source.BoardId,
            StoryPoints = source.StoryPoints,
            Priority = source.Priority,
            SynitiOwner = null,
            BusinessOwner = null,
            Status = null,
            ChangeReason = source.ChangeReason,
        };

    private static async Task TryEnsureEmbeddingAsync(
        ICortexEmbeddingService? cortexEmbeddingService,
        string ticketId,
        ILogger<TicketHandlersLogCategory> logger,
        CancellationToken cancellationToken)
    {
        if (cortexEmbeddingService is null)
        {
            return;
        }

        try
        {
            await cortexEmbeddingService.EnsureEmbeddingAsync(ticketId, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Cortex Memory embedding refresh failed for ticket {TicketId}. Continuing without blocking the ticket workflow.",
                ticketId);
        }
    }
}

/// <summary>
/// Log category for <see cref="TicketHandlers"/> (required because <c>ILogger&lt;T&gt;</c> cannot use a static class as <c>T</c>).
/// </summary>
public sealed class TicketHandlersLogCategory;
