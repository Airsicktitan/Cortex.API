using Cortex.API.Data;
using Cortex.API.Data.Repositories;
using Cortex.API.DTO;
using Cortex.API.Models;
using Cortex.API.Services;
using Microsoft.Extensions.Logging;
using Moq;

namespace Cortex.API.Tests;

public class NotificationServiceCommentNotificationTests
{
    [Fact]
    public async Task CreateCommentNotificationsAsync_NotifiesDistinctOwners_AndPublishesTargetedRealtimeEvents()
    {
        var actor = new User
        {
            Id = 3,
            DisplayName = "Commenter",
            Email = "commenter@test.com",
        };
        var synitiOwner = new User
        {
            Id = 1,
            DisplayName = "Syniti Owner",
            Email = "syniti@test.com",
        };
        var businessOwner = new User
        {
            Id = 2,
            DisplayName = "Business Owner",
            Email = "business@test.com",
        };
        var ticket = new Ticket
        {
            Id = "5001",
            Title = "Comment target",
            CreatedBy = actor.Id,
            SynitiOwner = synitiOwner.DisplayName,
            BusinessOwner = businessOwner.DisplayName,
        };

        List<UserNotification>? storedNotifications = null;
        var notificationRepository = new Mock<INotificationRepository>(MockBehavior.Strict);
        notificationRepository
            .Setup(repository => repository.AddRangeAsync(It.IsAny<IEnumerable<UserNotification>>()))
            .Callback<IEnumerable<UserNotification>>(notifications =>
            {
                storedNotifications = notifications.ToList();
                var createdAt = new DateTime(2026, 4, 18, 12, 0, 0, DateTimeKind.Utc);
                for (var index = 0; index < storedNotifications.Count; index += 1)
                {
                    storedNotifications[index].Id = 100 + index;
                    storedNotifications[index].CreatedDateUtc = createdAt.AddMinutes(index);
                }
            })
            .Returns(Task.CompletedTask);
        notificationRepository
            .Setup(repository => repository.SaveChangesAsync())
            .Returns(Task.CompletedTask);
        notificationRepository
            .Setup(repository => repository.GetUnreadCountAsync(It.IsAny<int>()))
            .ReturnsAsync((int userId) => userId == synitiOwner.Id ? 4 : 2);

        var userRepository = new Mock<IUserRepository>(MockBehavior.Strict);
        userRepository
            .Setup(repository => repository.GetAllUsersAsync())
            .ReturnsAsync([synitiOwner, businessOwner, actor]);

        var realtimeMessages = new List<RealtimeEventMessage>();
        var realtimeEventService = new Mock<IRealtimeEventService>(MockBehavior.Strict);
        realtimeEventService
            .Setup(service => service.PublishAsync(It.IsAny<RealtimeEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<RealtimeEventMessage, CancellationToken>((message, _) => realtimeMessages.Add(message))
            .Returns(ValueTask.CompletedTask);

        var notificationDeliveryService = new Mock<INotificationDeliveryService>(MockBehavior.Strict);
        var notificationChannelConfigurationService =
            new Mock<INotificationChannelConfigurationService>(MockBehavior.Strict);

        var service = new NotificationService(
            notificationRepository.Object,
            userRepository.Object,
            Mock.Of<ITicketRepository>(),
            Mock.Of<ISlaConfigurationService>(),
            notificationChannelConfigurationService.Object,
            notificationDeliveryService.Object,
            realtimeEventService.Object,
            Mock.Of<ILogger<NotificationService>>());

        var createdCount = await service.CreateCommentNotificationsAsync(ticket, actor);

        Assert.Equal(2, createdCount);
        Assert.NotNull(storedNotifications);
        Assert.Collection(
            storedNotifications!
                .Select(notification => notification.UserId)
                .OrderBy(id => id),
            userId => Assert.Equal(1, userId),
            userId => Assert.Equal(2, userId));
        Assert.All(storedNotifications, notification =>
        {
            Assert.Equal("Comment", notification.Category);
            Assert.Equal("ticket.comment", notification.EventType);
            Assert.Equal(ticket.Id, notification.TicketId);
            Assert.Contains(actor.DisplayName!, notification.Message);
        });

        Assert.Collection(
            realtimeMessages
                .Select(message => Assert.Single(message.RecipientUserIds!))
                .OrderBy(id => id),
            userId => Assert.Equal(1, userId),
            userId => Assert.Equal(2, userId));
        Assert.All(realtimeMessages, message =>
        {
            Assert.Equal("notification.created", message.EventType);
            Assert.Equal(Assert.Single(message.RecipientUserIds!), Assert.Single(message.AudienceUserIds!));
            var notification = Assert.Single(message.Notifications!);
            Assert.Equal("comment", notification.Type);
            Assert.Equal(ticket.Id, notification.TicketId);
            Assert.False(notification.IsRead);
        });

        notificationDeliveryService.VerifyNoOtherCalls();
        notificationChannelConfigurationService.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateCommentNotificationsAsync_SkipsSelfNotifications()
    {
        var actor = new User
        {
            Id = 3,
            DisplayName = "Commenter",
            Email = "commenter@test.com",
        };
        var businessOwner = new User
        {
            Id = 2,
            DisplayName = "Business Owner",
            Email = "business@test.com",
        };
        var ticket = new Ticket
        {
            Id = "5002",
            Title = "Skip self",
            CreatedBy = actor.Id,
            SynitiOwner = actor.DisplayName,
            BusinessOwner = businessOwner.DisplayName,
        };

        List<UserNotification>? storedNotifications = null;
        var notificationRepository = new Mock<INotificationRepository>(MockBehavior.Strict);
        notificationRepository
            .Setup(repository => repository.AddRangeAsync(It.IsAny<IEnumerable<UserNotification>>()))
            .Callback<IEnumerable<UserNotification>>(notifications =>
            {
                storedNotifications = notifications.ToList();
                if (storedNotifications.Count == 1)
                {
                    storedNotifications[0].Id = 200;
                    storedNotifications[0].CreatedDateUtc = new DateTime(2026, 4, 18, 12, 30, 0, DateTimeKind.Utc);
                }
            })
            .Returns(Task.CompletedTask);
        notificationRepository
            .Setup(repository => repository.SaveChangesAsync())
            .Returns(Task.CompletedTask);
        notificationRepository
            .Setup(repository => repository.GetUnreadCountAsync(businessOwner.Id))
            .ReturnsAsync(1);

        var userRepository = new Mock<IUserRepository>(MockBehavior.Strict);
        userRepository
            .Setup(repository => repository.GetAllUsersAsync())
            .ReturnsAsync([actor, businessOwner]);

        var realtimeMessages = new List<RealtimeEventMessage>();
        var realtimeEventService = new Mock<IRealtimeEventService>(MockBehavior.Strict);
        realtimeEventService
            .Setup(service => service.PublishAsync(It.IsAny<RealtimeEventMessage>(), It.IsAny<CancellationToken>()))
            .Callback<RealtimeEventMessage, CancellationToken>((message, _) => realtimeMessages.Add(message))
            .Returns(ValueTask.CompletedTask);

        var service = new NotificationService(
            notificationRepository.Object,
            userRepository.Object,
            Mock.Of<ITicketRepository>(),
            Mock.Of<ISlaConfigurationService>(),
            Mock.Of<INotificationChannelConfigurationService>(),
            Mock.Of<INotificationDeliveryService>(),
            realtimeEventService.Object,
            Mock.Of<ILogger<NotificationService>>());

        var createdCount = await service.CreateCommentNotificationsAsync(ticket, actor);

        Assert.Equal(1, createdCount);
        Assert.NotNull(storedNotifications);
        Assert.Single(storedNotifications!);
        Assert.Equal(businessOwner.Id, storedNotifications[0].UserId);
        Assert.DoesNotContain(storedNotifications, notification => notification.UserId == actor.Id);
        Assert.Single(realtimeMessages);
        Assert.Equal(businessOwner.Id, Assert.Single(realtimeMessages[0].RecipientUserIds!));
    }
}
