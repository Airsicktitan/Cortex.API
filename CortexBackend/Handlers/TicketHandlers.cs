namespace Cortex.API.Handlers;

using Cortex.API.Models;
using Cortex.API.Data;
using Cortex.API.DTO;
using Cortex.API.Services;
using Cortex.API.Validation;

using Microsoft.Extensions.Logging;

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
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        ISlaConfigurationService slaConfigurationService,
        IResponseMappingContextFactory mappingContextFactory)
    {
        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        var tickets = await repo.GetAllTicketsAsync(sinceUtc?.UtcDateTime);
        var visibleTickets = tickets.Where(visibilityContext.CanView).ToList();
        var slaConfigurations = await slaConfigurationService.GetPriorityMapAsync();

        var mappingContext = await mappingContextFactory.CreateAsync(
            visibleTickets.Select(ticket => ticket.CreatedBy),
            null,
            visibleTickets.Select(ticket => ticket.BoardId));

        return Results.Ok(
            visibleTickets.Select(ticket => ticket.ToResponse(slaConfigurations, mappingContext)));
    }

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

    public static async Task<IResult> GetArchivedTickets(
        DateTimeOffset? sinceUtc,
        ITicketRepository repo,
        ITicketVisibilityService ticketVisibilityService,
        IResponseMappingContextFactory mappingContextFactory)
    {
        var visibilityContext = await ticketVisibilityService.GetCurrentVisibilityAsync();
        var archivedTickets = await repo.GetArchivedTicketsAsync(sinceUtc?.UtcDateTime);
        var visibleTickets = archivedTickets.Where(ticket =>
            visibilityContext.CanView(ticket.CreatedBy, ticket.SynitiOwner, ticket.BusinessOwner)).ToList();

        var mappingContext = await mappingContextFactory.CreateAsync(
            visibleTickets.SelectMany(ticket => new[] { ticket.CreatedBy, ticket.ArchivedBy }),
            null,
            visibleTickets.Select(ticket => ticket.BoardId));

        return Results.Ok(visibleTickets.Select(ticket => ticket.ToResponse(mappingContext)));
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
            var routingDepartment = NormalizeOptionalValue(request.Department)
                ?? NormalizeOptionalValue(currentUser.Department);
            var routingResolution = await ticketRoutingRuleService.ResolveOwnersAsync(
                routingDepartment,
                request.Title);
            var resolvedSynitiOwner = NormalizeOptionalValue(request.SynitiOwner)
                ?? routingResolution.SynitiOwner;
            var resolvedBusinessOwner = NormalizeOptionalValue(request.BusinessOwner)
                ?? routingResolution.BusinessOwner
                ?? GetDefaultBusinessOwner(currentUser);

            var selectedBoard = await ResolveBoardForCreateAsync(ticketBoardService, request.BoardId);
            var storyPoints = ResolveStoryPoints(
                selectedBoard,
                request.StoryPoints,
                null);

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
        ISlaConfigurationService slaConfigurationService,
        ITicketStatusService ticketStatusService,
        ITicketBoardService ticketBoardService,
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
            existing.SynitiOwner = request.SynitiOwner ?? existing.SynitiOwner;
            existing.BusinessOwner = request.BusinessOwner ?? existing.BusinessOwner;
            existing.LastModifiedBy = currentUser.Id;
            existing.LastModifiedDate = DateTime.UtcNow;

            await repo.UpdateTicketAsync(existing);
            await repo.SaveChangesAsync();

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

    public static async Task<IResult> DeleteTicket(string id, ITicketRepository repo)
    {
        var normalizedId = id.Trim();
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return Results.BadRequest("Invalid ticket id.");
        }

        var deleted = await repo.DeleteTicketAsync(normalizedId);

        if (!deleted)
            return Results.NotFound();

        await repo.SaveChangesAsync();

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
            LastModifiedDate = ticket.LastModifiedDate
        };
    }

    private static string? NormalizeOptionalValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
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
