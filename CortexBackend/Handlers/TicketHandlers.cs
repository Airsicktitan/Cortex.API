namespace Cortex.API.Handlers;

using Cortex.API.Models;
using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Services;
using Cortex.API.Validation;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Globalization;

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
        IResponseMappingContextFactory mappingContextFactory)
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

            var items = tickets
                .Select(ticket => ticket.ToResponse(slaConfigurations, mappingContext))
                .ToList();

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

            var responses = ordered
                .Select(ticket => ticket.ToResponse(slaConfigurations, mappingContext))
                .ToList();

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

            var responses = tickets
                .Select(ticket => ticket.ToResponse(slaConfigurations, mappingContext))
                .ToList();
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

        var pagedResponses = pageTickets
            .Select(ticket => ticket.ToResponse(slaConfigurations, pageMappingContext))
            .ToList();

        return Results.Ok(new PagedTicketListResponse
        {
            Items = pagedResponses,
            Page = normalizedPage,
            PageSize = normalizedPageSize,
            TotalCount = total,
            TotalPages = ComputeTotalPages(total, normalizedPageSize)
        });
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

    public static async Task<IResult> GetTicketById(
        string id,
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        ISlaConfigurationService slaConfigurationService,
        IResponseMappingContextFactory mappingContextFactory)
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

        return Results.Ok(ticket.ToResponse(slaConfigurations, mappingContext));
    }

    public static async Task<IResult> GetTicketHistory(
        string id,
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        ITicketAuditService ticketAuditService,
        IResponseMappingContextFactory mappingContextFactory)
    {
        var ticket = await repo.GetTicketByIdAsync(id);
        if (ticket is null)
        {
            var archivedTicket = await repo.GetArchivedTicketByIdAsync(id);
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

        var history = (await ticketAuditService.GetTicketHistoryAsync(id)).ToList();
        var mappingContext = await mappingContextFactory.CreateAsync(
            history.Select(entry => entry.ChangedBy));
        return Results.Ok(history.Select(entry => entry.ToResponse(mappingContext)));
    }

    public static async Task<IResult> GetLatestRoutingDecision(
        string id,
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        ITicketRoutingRuleService ticketRoutingRuleService)
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

        var decision = await ticketRoutingRuleService.GetLatestDecisionAsync(id);
        var @override = await ticketRoutingRuleService.GetLatestOverrideAsync(id);
        return Results.Ok(new TicketRoutingLatestResponse
        {
            Decision = decision?.ToResponse(),
            Override = @override?.ToResponse()
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
        IResponseMappingContextFactory mappingContextFactory)
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

        return Results.Ok(visibleTickets.Select(ticket => ticket.ToResponse(slaConfigurations, mappingContext)));
    }

    public static async Task<IResult> GetTicketsByPriority(
        string priority,
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        ISlaConfigurationService slaConfigurationService,
        IResponseMappingContextFactory mappingContextFactory)
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

        return Results.Ok(visibleTickets.Select(ticket => ticket.ToResponse(slaConfigurations, mappingContext)));
    }

    public static async Task<IResult> GetTicketsByUser(
        IUserContextService userContext,
        ITicketRepository repo,
        ISlaConfigurationService slaConfigurationService,
        IResponseMappingContextFactory mappingContextFactory)
    {
        var currentUser = await userContext.GetCurrentUserAsync();
        var tickets = (await repo.GetTicketByUserAsync(currentUser.Id)).ToList();
        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();

        var mappingContext = await mappingContextFactory.CreateAsync(
            tickets.Select(ticket => ticket.CreatedBy),
            null,
            tickets.Select(ticket => ticket.BoardId));

        return Results.Ok(tickets.Select(ticket => ticket.ToResponse(slaConfigurations, mappingContext)));
    }

    public static async Task<IResult> CreateTicket(
        CreateTicketRequest request,
        ITicketRepository repo,
        IUserContextService userContext,
        IUserRepository userRepository,
        ISlaConfigurationService slaConfigurationService,
        ITicketStatusService ticketStatusService,
        ITicketBoardService ticketBoardService,
        ITicketRoutingRuleService ticketRoutingRuleService,
        ITicketAuditService ticketAuditService,
        INotificationService notificationService,
        IRealtimeEventService realtimeEventService,
        IRealtimeAudienceResolver realtimeAudienceResolver,
        IResponseMappingContextFactory mappingContextFactory,
        ILogger<TicketHandlersLogCategory> logger)
    {
        try
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
            var resolvedSynitiOwner = manualSynitiOwner ?? routingDecision.RecommendedSynitiOwner;
            var resolvedBusinessOwner = manualBusinessOwner
                ?? routingDecision.RecommendedBusinessOwner
                ?? GetDefaultBusinessOwner(currentUser);

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
                CreatedBy = currentUser.Id,
                CreatedDate = DateTime.UtcNow
            };

            await repo.CreateTicketAsync(ticket);
            await repo.SaveChangesAsync();

            var createdTicket = await repo.GetTicketByIdAsync(ticket.Id);

            if (createdTicket is null)
                return Results.Problem("Ticket was created but could not be retrieved.");

            await ticketRoutingRuleService.RecordDecisionAsync(createdTicket.Id, routingDecision);
            if (HasOwnerOverride(
                    routingDecision.RecommendedSynitiOwner,
                    routingDecision.RecommendedBusinessOwner,
                    resolvedSynitiOwner,
                    resolvedBusinessOwner))
            {
                await ticketRoutingRuleService.RecordOverrideAsync(
                    ticketId: createdTicket.Id,
                    overriddenByUserId: currentUser.Id,
                    previousSynitiOwner: routingDecision.RecommendedSynitiOwner,
                    previousBusinessOwner: routingDecision.RecommendedBusinessOwner,
                    newSynitiOwner: resolvedSynitiOwner,
                    newBusinessOwner: resolvedBusinessOwner,
                    reasonType: ParseOverrideReasonType(request.ChangeReason),
                    reasonText: request.ChangeReason);
            }

            var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();
            var mappingContext = await mappingContextFactory.CreateAsync(
                [createdTicket.CreatedBy],
                null,
                [createdTicket.BoardId]);
            var createdTicketResponse = createdTicket.ToResponse(slaConfigurations, mappingContext);
            var audienceUserIds = await realtimeAudienceResolver.GetAudienceUserIdsAsync(createdTicket);

            await ticketAuditService.RecordTicketCreatedAsync(
                createdTicket,
                currentUser,
                request.ChangeReason);
            await notificationService.CreateAssignmentNotificationsForNewTicketAsync(
                createdTicket,
                currentUser);
            await realtimeEventService.PublishAsync(new RealtimeEventMessage
            {
                EventType = "ticket.created",
                TicketId = createdTicket.Id,
                EntityId = createdTicket.Id,
                AudienceUserIds = audienceUserIds,
                Ticket = createdTicketResponse
            });
            await realtimeEventService.PublishAsync(new RealtimeEventMessage
            {
                EventType = "ticket.routed",
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

            return Results.Created(
                $"/api/tickets/{createdTicket.Id}",
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
    }

    public static async Task<IResult> UpdateTicket(
        string id,
        UpdateTicketRequest request,
        ITicketRepository repo,
        IUserContextService userContext,
        IUserRepository userRepository,
        ISlaConfigurationService slaConfigurationService,
        ITicketStatusService ticketStatusService,
        ITicketBoardService ticketBoardService,
        ITicketRoutingRuleService ticketRoutingRuleService,
        ITicketAuditService ticketAuditService,
        INotificationService notificationService,
        IRealtimeEventService realtimeEventService,
        IRealtimeAudienceResolver realtimeAudienceResolver,
        IResponseMappingContextFactory mappingContextFactory,
        ILogger<TicketHandlersLogCategory> logger)
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
            if (routingFactorsChanged)
            {
                routingDecision = await ticketRoutingRuleService.EvaluateAsync(
                    BuildRoutingFactors(
                        boardId: targetBoard.Id,
                        priority: resolvedPriority,
                        requesterDepartment: requesterDepartment,
                        requesterRole: requesterRole,
                        legacyDepartment: legacyDepartment,
                        legacyTitle: resolvedTitle));
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

            if (routingDecision is not null
                && HasOwnerOverride(
                    routingDecision.RecommendedSynitiOwner,
                    routingDecision.RecommendedBusinessOwner,
                    resolvedSynitiOwner,
                    resolvedBusinessOwner))
            {
                await ticketRoutingRuleService.RecordOverrideAsync(
                    ticketId: existing.Id,
                    overriddenByUserId: currentUser.Id,
                    previousSynitiOwner: routingDecision.RecommendedSynitiOwner,
                    previousBusinessOwner: routingDecision.RecommendedBusinessOwner,
                    newSynitiOwner: resolvedSynitiOwner,
                    newBusinessOwner: resolvedBusinessOwner,
                    reasonType: ParseOverrideReasonType(request.ChangeReason),
                    reasonText: request.ChangeReason);
            }

            existing.SynitiOwner = resolvedSynitiOwner;
            existing.BusinessOwner = resolvedBusinessOwner;
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

            var updatedTicket = await repo.GetTicketByIdAsync(id);

            if (updatedTicket is null)
                return Results.Problem("Ticket was updated but could not be retrieved.");

            var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();
            var mappingContext = await mappingContextFactory.CreateAsync(
                [updatedTicket.CreatedBy],
                null,
                [updatedTicket.BoardId]);
            var updatedTicketResponse = updatedTicket.ToResponse(slaConfigurations, mappingContext);
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
        var restoredTicketResponse = restoredTicket.ToResponse(slaConfigurations, mappingContext);
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

    private static Ticket CloneTicket(Ticket ticket)
    {
        return new Ticket
        {
            Id = ticket.Id,
            Title = ticket.Title,
            Description = ticket.Description,
            Status = ticket.Status,
            Priority = ticket.Priority,
            BoardId = ticket.BoardId,
            StoryPoints = ticket.StoryPoints,
            SynitiOwner = ticket.SynitiOwner,
            BusinessOwner = ticket.BusinessOwner,
            CreatedBy = ticket.CreatedBy,
            CreatedDate = ticket.CreatedDate,
            LastModifiedBy = ticket.LastModifiedBy,
            LastModifiedDate = ticket.LastModifiedDate,
            RowVersion = ticket.RowVersion is { Length: > 0 }
                ? (byte[])ticket.RowVersion.Clone()
                : [],
        };
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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

    private static string? GetDefaultBusinessOwner(User user)
    {
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            return user.DisplayName.Trim();
        }

        return NormalizeOptionalValue(user.Email);
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
}

/// <summary>
/// Log category for <see cref="TicketHandlers"/> (required because <c>ILogger&lt;T&gt;</c> cannot use a static class as <c>T</c>).
/// </summary>
public sealed class TicketHandlersLogCategory;
