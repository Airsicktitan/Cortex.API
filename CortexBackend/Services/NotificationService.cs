using Cortex.API.Data;
using Cortex.API.Data.Repositories;
using Cortex.API.DTO;
using Cortex.API.Models;

namespace Cortex.API.Services;

public class NotificationService(
    INotificationRepository notificationRepository,
    IUserRepository userRepository,
    ITicketRepository ticketRepository,
    ISlaConfigurationService slaConfigurationService,
    INotificationChannelConfigurationService notificationChannelConfigurationService,
    INotificationDeliveryService notificationDeliveryService,
    IRealtimeEventService realtimeEventService,
    ILogger<NotificationService> logger) : INotificationService
{
    private readonly INotificationRepository _notificationRepository = notificationRepository;
    private readonly IUserRepository _userRepository = userRepository;
    private readonly ITicketRepository _ticketRepository = ticketRepository;
    private readonly ISlaConfigurationService _slaConfigurationService = slaConfigurationService;
    private readonly INotificationChannelConfigurationService _notificationChannelConfigurationService =
        notificationChannelConfigurationService;
    private readonly INotificationDeliveryService _notificationDeliveryService =
        notificationDeliveryService;
    private readonly IRealtimeEventService _realtimeEventService = realtimeEventService;
    private readonly ILogger<NotificationService> _logger = logger;

    public async Task<NotificationFeedResponse> GetFeedAsync(int userId, int take = 20)
    {
        var items = await _notificationRepository.GetRecentByUserIdAsync(userId, Math.Max(1, take));
        var unreadCount = await _notificationRepository.GetUnreadCountAsync(userId);

        return new NotificationFeedResponse
        {
            UnreadCount = unreadCount,
            Items = items.Select(ToResponse).ToList()
        };
    }

    public async Task<NotificationResponse?> MarkAsReadAsync(int userId, int notificationId)
    {
        var notification = await _notificationRepository.GetByIdAsync(notificationId);
        if (notification is null || notification.UserId != userId)
        {
            return null;
        }

        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadDateUtc = DateTime.UtcNow;
            await _notificationRepository.SaveChangesAsync();
        }

        return ToResponse(notification);
    }

    public async Task MarkAllAsReadAsync(int userId)
    {
        var unreadNotifications = await _notificationRepository.GetUnreadByUserIdAsync(userId);
        if (unreadNotifications.Count == 0)
        {
            return;
        }

        var utcNow = DateTime.UtcNow;
        foreach (var notification in unreadNotifications)
        {
            notification.IsRead = true;
            notification.ReadDateUtc = utcNow;
        }

        await _notificationRepository.SaveChangesAsync();
    }

    public async Task<int> CreateAssignmentNotificationsForNewTicketAsync(Ticket ticket, User actor)
    {
        var originalTicket = new Ticket
        {
            Id = ticket.Id,
            Title = ticket.Title,
            SynitiOwner = null,
            BusinessOwner = null
        };

        return await CreateAssignmentNotificationsAsync(originalTicket, ticket, actor);
    }

    public async Task<int> CreateAssignmentNotificationsAsync(Ticket originalTicket, Ticket updatedTicket, User actor)
    {
        var users = await GetNotifiableUsersAsync();
        var aliases = OwnerFieldResolution.BuildAliasLookup(users);
        var assignments = new Dictionary<int, AssignmentNotificationState>();

        AddAssignment(assignments, aliases, actor, originalTicket.SynitiOwner, updatedTicket.SynitiOwner, "Syniti Owner", updatedTicket.Id);
        AddAssignment(assignments, aliases, actor, originalTicket.BusinessOwner, updatedTicket.BusinessOwner, "Business Owner", updatedTicket.Id);

        if (assignments.Count == 0)
        {
            return 0;
        }

        var actorName = FormatActorName(actor);
        var notifications = assignments.Values.Select(assignment =>
        {
            var joinedRoles = JoinWithAnd(assignment.Roles);
            var message = assignment.User.Id == actor.Id
                ? $"You are assigned as {joinedRoles} on ticket {updatedTicket.Id} ({updatedTicket.Title})."
                : $"{actorName} assigned you as {joinedRoles} on ticket {updatedTicket.Id} ({updatedTicket.Title}).";
            return new UserNotification
            {
                UserId = assignment.User.Id,
                TicketId = updatedTicket.Id,
                TicketIsArchived = false,
                Category = "Assignment",
                EventType = "ticket.assignment",
                Severity = "info",
                Title = $"Ticket {updatedTicket.Id} assigned to you",
                Message = message
            };
        }).ToList();

        var createdCount = await CreateAndPublishAsync(notifications);
        if (createdCount > 0)
        {
            var recipientsById = assignments.Values.ToDictionary(
                assignment => assignment.User.Id,
                assignment => assignment.User);
            await DeliverExternalNotificationsAsync(
                notifications,
                recipientsById);
        }

        return createdCount;
    }

    public async Task<int> CreateCommentNotificationsAsync(Ticket ticket, User actor)
    {
        var users = await GetNotifiableUsersAsync();
        var recipients = ResolveOwnerRecipients(ticket, users)
            .Where(user => user.Id != actor.Id)
            .ToList();

        if (recipients.Count == 0)
        {
            return 0;
        }

        var actorName = FormatActorName(actor);
        var notifications = recipients.Select(user => new UserNotification
        {
            UserId = user.Id,
            TicketId = ticket.Id,
            TicketIsArchived = false,
            Category = "Comment",
            EventType = "ticket.comment",
            Severity = "info",
            Title = $"New comment on ticket {ticket.Id}",
            Message = $"{actorName} commented on ticket {ticket.Id} ({ticket.Title})."
        }).ToList();

        return await CreateAndPublishAsync(notifications);
    }

    public async Task<int> CreateArchiveNotificationsAsync(
        Ticket ticket,
        User actor,
        bool ticketIsArchived,
        bool isReactivated = false)
    {
        var users = await GetNotifiableUsersAsync();
        var recipients = ResolveInterestedUsers(ticket, users).Where(user => user.Id != actor.Id).ToList();
        if (recipients.Count == 0)
        {
            return 0;
        }

        var actorName = FormatActorName(actor);
        var eventType = isReactivated ? "ticket.reactivated" : "ticket.archived";
        var title = isReactivated
            ? $"Ticket {ticket.Id} was reactivated"
            : $"Ticket {ticket.Id} was archived";
        var message = isReactivated
            ? $"{actorName} reactivated ticket {ticket.Id} ({ticket.Title})."
            : $"{actorName} archived ticket {ticket.Id} ({ticket.Title}).";

        var notifications = recipients.Select(user => new UserNotification
        {
            UserId = user.Id,
            TicketId = ticket.Id,
            TicketIsArchived = ticketIsArchived,
            Category = "Archive",
            EventType = eventType,
            Severity = "info",
            Title = title,
            Message = message
        }).ToList();

        return await CreateAndPublishAsync(notifications);
    }

    public async Task<int> ProcessSlaNotificationsAsync(
        DateTime utcNow,
        CancellationToken cancellationToken = default)
    {
        var users = await GetNotifiableUsersAsync(utcNow);
        if (users.Count == 0)
        {
            return 0;
        }

        var usersById = users.ToDictionary(user => user.Id);
        var aliases = OwnerFieldResolution.BuildAliasLookup(users);
        var tickets = await _ticketRepository.GetAllTicketsAsync();
        var slaConfigurations = await _slaConfigurationService.GetPriorityMapAsync();
        var notifications = new List<UserNotification>();

        foreach (var ticket in tickets)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (ticket.ApprovalStatus != ApprovalStatus.Approved)
            {
                continue;
            }

            if (!usersById.ContainsKey(ticket.CreatedBy) &&
                string.IsNullOrWhiteSpace(ticket.SynitiOwner) &&
                string.IsNullOrWhiteSpace(ticket.BusinessOwner))
            {
                continue;
            }

            slaConfigurations.TryGetValue(ticket.Priority, out var configuration);
            var snapshot = TicketSlaCalculator.Calculate(ticket, configuration, utcNow);
            if (snapshot.Status is not "At Risk" and not "Breached")
            {
                continue;
            }

            var recipients = ResolveInterestedUsers(ticket, usersById, aliases);
            if (recipients.Count == 0)
            {
                continue;
            }

            var title = snapshot.Status == "Breached"
                ? $"Ticket {ticket.Id} breached SLA"
                : $"Ticket {ticket.Id} is at risk";
            var message = snapshot.Status == "Breached"
                ? $"Ticket {ticket.Id} ({ticket.Title}) has breached its SLA target."
                : $"Ticket {ticket.Id} ({ticket.Title}) is approaching its SLA target.";
            var severity = snapshot.Status == "Breached" ? "critical" : "warning";
            var deduplicationKey = BuildSlaDeduplicationKey(ticket.Id, snapshot.Status);

            foreach (var recipient in recipients)
            {
                if (await _notificationRepository.ExistsAsync(recipient.Id, deduplicationKey))
                {
                    continue;
                }

                notifications.Add(new UserNotification
                {
                    UserId = recipient.Id,
                    TicketId = ticket.Id,
                    TicketIsArchived = false,
                    Category = "SLA",
                    EventType = snapshot.Status == "Breached" ? "sla.breached" : "sla.at-risk",
                    Severity = severity,
                    Title = title,
                    Message = message,
                    DeduplicationKey = deduplicationKey
                });
            }
        }

        var createdCount = await CreateAndPublishAsync(notifications);
        if (createdCount > 0)
        {
            var recipientsById = notifications
                .Select(notification => notification.UserId)
                .Distinct()
                .Where(usersById.ContainsKey)
                .ToDictionary(userId => userId, userId => usersById[userId]);
            await DeliverExternalNotificationsAsync(
                notifications,
                recipientsById,
                cancellationToken);
        }

        return createdCount;
    }

    private async Task<int> CreateAndPublishAsync(IReadOnlyList<UserNotification> notifications)
    {
        if (notifications.Count == 0)
        {
            return 0;
        }

        await _notificationRepository.AddRangeAsync(notifications);
        await _notificationRepository.SaveChangesAsync();

        var notificationsByUserId = notifications
            .GroupBy(notification => notification.UserId)
            .ToList();

        foreach (var notificationGroup in notificationsByUserId)
        {
            var userId = notificationGroup.Key;
            var notificationResponses = notificationGroup
                .OrderByDescending(notification => notification.CreatedDateUtc)
                .Select(ToResponse)
                .ToArray();

            await _realtimeEventService.PublishAsync(new RealtimeEventMessage
            {
                EventType = "notification.created",
                EntityId = notificationResponses[0].Id.ToString(),
                TicketId = notificationResponses
                    .Select(notification => notification.TicketId)
                    .FirstOrDefault(ticketId => !string.IsNullOrWhiteSpace(ticketId)),
                RecipientUserIds = [userId],
                AudienceUserIds = [userId],
                Notifications = notificationResponses,
                UnreadCount = await _notificationRepository.GetUnreadCountAsync(userId)
            });
        }

        return notifications.Count;
    }

    private async Task DeliverExternalNotificationsAsync(
        IReadOnlyList<UserNotification> notifications,
        IReadOnlyDictionary<int, User> recipientsById,
        CancellationToken cancellationToken = default)
    {
        if (notifications.Count == 0 || recipientsById.Count == 0)
        {
            return;
        }

        var defaultConfiguration = await _notificationChannelConfigurationService.GetAsync();
        var notificationsByUser = notifications
            .GroupBy(notification => notification.UserId)
            .ToList();

        foreach (var group in notificationsByUser)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!recipientsById.TryGetValue(group.Key, out var recipient))
            {
                continue;
            }

            var channelMode = ResolveChannelMode(
                recipient,
                group.First().Category,
                defaultConfiguration);
            if (channelMode == NotificationChannelMode.Neither)
            {
                continue;
            }

            var notificationBatch = group.ToList();
            await _notificationDeliveryService.DeliverAsync(
                channelMode,
                notificationBatch,
                new Dictionary<int, User>
                {
                    [recipient.Id] = recipient
                },
                cancellationToken);
        }
    }

    private async Task<List<User>> GetNotifiableUsersAsync(DateTime? utcNow = null)
    {
        var effectiveUtcNow = utcNow ?? DateTime.UtcNow;
        return (await _userRepository.GetAllUsersAsync())
            .Where(user =>
                user.IsActive &&
                (user.ExpiryDate is null || user.ExpiryDate > effectiveUtcNow))
            .ToList();
    }

    private void AddAssignment(
        IDictionary<int, AssignmentNotificationState> assignments,
        IReadOnlyDictionary<string, User> aliases,
        User actor,
        string? previousOwner,
        string? nextOwner,
        string roleLabel,
        string ticketId)
    {
        if (string.IsNullOrWhiteSpace(nextOwner?.Trim()))
        {
            return;
        }

        var previousResolved = OwnerFieldResolution.ResolveUser(previousOwner, aliases);
        var nextResolved = OwnerFieldResolution.ResolveUser(nextOwner, aliases);

        if (nextResolved is null)
        {
            _logger.LogWarning(
                "Assignment notification skipped: could not resolve owner alias '{Owner}' for ticket {TicketId} role {RoleLabel}.",
                nextOwner,
                ticketId,
                roleLabel);
            return;
        }

        if (previousResolved?.Id == nextResolved.Id)
        {
            return;
        }

        if (!assignments.TryGetValue(nextResolved.Id, out var state))
        {
            state = new AssignmentNotificationState(nextResolved);
            assignments[nextResolved.Id] = state;
        }

        state.Roles.Add(roleLabel);
    }

    private static IReadOnlyList<User> ResolveInterestedUsers(
        Ticket ticket,
        IReadOnlyList<User> users)
    {
        var usersById = users.ToDictionary(user => user.Id);
        var aliases = OwnerFieldResolution.BuildAliasLookup(users);
        return ResolveInterestedUsers(ticket, usersById, aliases);
    }

    private static IReadOnlyList<User> ResolveOwnerRecipients(
        Ticket ticket,
        IReadOnlyList<User> users)
    {
        var aliases = OwnerFieldResolution.BuildAliasLookup(users);
        var recipients = new Dictionary<int, User>();

        AddOwnerRecipient(recipients, ticket.SynitiOwner, aliases);
        AddOwnerRecipient(recipients, ticket.BusinessOwner, aliases);

        return recipients.Values.ToList();
    }

    private static IReadOnlyList<User> ResolveInterestedUsers(
        Ticket ticket,
        IReadOnlyDictionary<int, User> usersById,
        IReadOnlyDictionary<string, User> aliases)
    {
        var recipients = new Dictionary<int, User>();

        if (usersById.TryGetValue(ticket.CreatedBy, out var creator))
        {
            recipients[creator.Id] = creator;
        }

        AddOwnerRecipient(recipients, ticket.SynitiOwner, aliases);
        AddOwnerRecipient(recipients, ticket.BusinessOwner, aliases);

        return recipients.Values.ToList();
    }

    private static void AddOwnerRecipient(
        IDictionary<int, User> recipients,
        string? rawOwner,
        IReadOnlyDictionary<string, User> aliases)
    {
        var user = OwnerFieldResolution.ResolveUser(rawOwner, aliases);
        if (user is not null)
        {
            recipients[user.Id] = user;
        }
    }

    private static string BuildSlaDeduplicationKey(string ticketId, string status)
    {
        var normalizedStatus = status.Replace(' ', '-').ToLowerInvariant();
        return $"sla:{normalizedStatus}:{ticketId}";
    }

    private static string FormatActorName(User actor)
    {
        return actor.DisplayName?.Trim()
            ?? actor.NickName?.Trim()
            ?? actor.Email;
    }

    private static string JoinWithAnd(IReadOnlyList<string> values)
    {
        return values.Count switch
        {
            0 => string.Empty,
            1 => values[0],
            2 => $"{values[0]} and {values[1]}",
            _ => $"{string.Join(", ", values.Take(values.Count - 1))}, and {values[^1]}"
        };
    }

    private static NotificationChannelMode ResolveChannelMode(
        User user,
        string category,
        NotificationChannelConfiguration defaultConfiguration)
    {
        return string.Equals(category, "SLA", StringComparison.OrdinalIgnoreCase)
            ? user.SlaRiskNotificationChannel ?? defaultConfiguration.SlaRiskChannel
            : user.AssignmentNotificationChannel ?? defaultConfiguration.AssignmentChannel;
    }

    private static NotificationResponse ToResponse(UserNotification notification)
    {
        return new NotificationResponse
        {
            Id = notification.Id,
            Type = ResolveNotificationType(notification),
            Category = notification.Category,
            EventType = notification.EventType,
            Severity = notification.Severity,
            Title = notification.Title,
            Message = notification.Message,
            TicketId = notification.TicketId,
            TicketIsArchived = notification.TicketIsArchived,
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedDateUtc,
            CreatedDateUtc = notification.CreatedDateUtc,
            ReadDateUtc = notification.ReadDateUtc
        };
    }

    private static string ResolveNotificationType(UserNotification notification)
    {
        if (string.Equals(notification.Category, "Assignment", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(notification.EventType, "ticket.assignment", StringComparison.OrdinalIgnoreCase))
        {
            return "assignment";
        }

        if (string.Equals(notification.Category, "Comment", StringComparison.OrdinalIgnoreCase) ||
            notification.EventType.Contains("comment", StringComparison.OrdinalIgnoreCase))
        {
            return "comment";
        }

        if (string.Equals(notification.Category, "Archive", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(notification.Category, "SLA", StringComparison.OrdinalIgnoreCase) ||
            notification.EventType.StartsWith("ticket.", StringComparison.OrdinalIgnoreCase) ||
            notification.EventType.StartsWith("sla.", StringComparison.OrdinalIgnoreCase))
        {
            return "status";
        }

        return "system";
    }

    private sealed class AssignmentNotificationState(User user)
    {
        public User User { get; } = user;
        public List<string> Roles { get; } = [];
    }
}
