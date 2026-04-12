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
    IRealtimeEventService realtimeEventService) : INotificationService
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
        var aliases = BuildUserAliasLookup(users);
        var assignments = new Dictionary<int, AssignmentNotificationState>();

        AddAssignment(assignments, aliases, actor, originalTicket.SynitiOwner, updatedTicket.SynitiOwner, "Syniti Owner");
        AddAssignment(assignments, aliases, actor, originalTicket.BusinessOwner, updatedTicket.BusinessOwner, "Business Owner");

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
        var aliases = BuildUserAliasLookup(users);
        var tickets = await _ticketRepository.GetAllTicketsAsync();
        var slaConfigurations = await _slaConfigurationService.GetPriorityMapAsync();
        var notifications = new List<UserNotification>();

        foreach (var ticket in tickets)
        {
            cancellationToken.ThrowIfCancellationRequested();

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

        await _realtimeEventService.PublishAsync(new RealtimeEventMessage
        {
            EventType = "notification.created",
            EntityId = notifications.Count.ToString(),
            TicketId = notifications.Select(notification => notification.TicketId).FirstOrDefault(ticketId => !string.IsNullOrWhiteSpace(ticketId)),
            RecipientUserIds = notifications.Select(notification => notification.UserId).Distinct().ToArray()
        });

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

    private static Dictionary<string, User> BuildUserAliasLookup(IEnumerable<User> users)
    {
        var aliases = new Dictionary<string, User>(StringComparer.OrdinalIgnoreCase);

        foreach (var user in users)
        {
            AddAlias(aliases, "email", user.Email, user);
            AddAlias(aliases, "display", user.DisplayName, user);
            AddAlias(aliases, "nickname", user.NickName, user);
        }

        return aliases;
    }

    private static void AddAssignment(
        IDictionary<int, AssignmentNotificationState> assignments,
        IReadOnlyDictionary<string, User> aliases,
        User actor,
        string? previousOwner,
        string? nextOwner,
        string roleLabel)
    {
        var previousNormalized = NormalizeOwner(previousOwner);
        var nextNormalized = NormalizeOwner(nextOwner);

        if (string.IsNullOrWhiteSpace(nextNormalized) ||
            string.Equals(previousNormalized, nextNormalized, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var resolvedUser = ResolveOwnerUser(nextOwner, aliases);
        if (resolvedUser is null)
        {
            return;
        }

        if (!assignments.TryGetValue(resolvedUser.Id, out var state))
        {
            state = new AssignmentNotificationState(resolvedUser);
            assignments[resolvedUser.Id] = state;
        }

        state.Roles.Add(roleLabel);
    }

    private static IReadOnlyList<User> ResolveInterestedUsers(
        Ticket ticket,
        IReadOnlyList<User> users)
    {
        var usersById = users.ToDictionary(user => user.Id);
        var aliases = BuildUserAliasLookup(users);
        return ResolveInterestedUsers(ticket, usersById, aliases);
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
        var user = ResolveOwnerUser(rawOwner, aliases);
        if (user is not null)
        {
            recipients[user.Id] = user;
        }
    }

    private static User? ResolveOwnerUser(
        string? rawOwner,
        IReadOnlyDictionary<string, User> aliases)
    {
        var normalized = NormalizeOwner(rawOwner);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return null;
        }

        if (normalized.Contains('@') &&
            aliases.TryGetValue($"email:{normalized}", out var byEmail))
        {
            return byEmail;
        }

        if (aliases.TryGetValue($"display:{normalized}", out var byDisplayName))
        {
            return byDisplayName;
        }

        if (aliases.TryGetValue($"nickname:{normalized}", out var byNickname))
        {
            return byNickname;
        }

        if (aliases.TryGetValue($"email:{normalized}", out byEmail))
        {
            return byEmail;
        }

        return null;
    }

    private static void AddAlias(
        IDictionary<string, User> aliases,
        string prefix,
        string? rawValue,
        User user)
    {
        var normalized = NormalizeOwner(rawValue);
        if (string.IsNullOrWhiteSpace(normalized) || aliases.ContainsKey($"{prefix}:{normalized}"))
        {
            return;
        }

        aliases[$"{prefix}:{normalized}"] = user;
    }

    private static string NormalizeOwner(string? rawValue)
    {
        return string.IsNullOrWhiteSpace(rawValue)
            ? string.Empty
            : rawValue.Trim().ToLowerInvariant();
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
            Category = notification.Category,
            EventType = notification.EventType,
            Severity = notification.Severity,
            Title = notification.Title,
            Message = notification.Message,
            TicketId = notification.TicketId,
            TicketIsArchived = notification.TicketIsArchived,
            IsRead = notification.IsRead,
            CreatedDateUtc = notification.CreatedDateUtc,
            ReadDateUtc = notification.ReadDateUtc
        };
    }

    private sealed class AssignmentNotificationState(User user)
    {
        public User User { get; } = user;
        public List<string> Roles { get; } = [];
    }
}
