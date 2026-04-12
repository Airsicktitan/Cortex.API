using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using Cortex.API.Models;
using Microsoft.Extensions.Options;

namespace Cortex.API.Services;

public class NotificationDeliveryService(
    HttpClient httpClient,
    IOptions<EmailNotificationOptions> emailOptions,
    IOptions<TeamsNotificationOptions> teamsOptions,
    ILogger<NotificationDeliveryService> logger)
    : INotificationDeliveryService
{
    private readonly HttpClient _httpClient = httpClient;
    private readonly EmailNotificationOptions _emailOptions = emailOptions.Value;
    private readonly TeamsNotificationOptions _teamsOptions = teamsOptions.Value;
    private readonly ILogger<NotificationDeliveryService> _logger = logger;

    public async Task DeliverAsync(
        NotificationChannelMode mode,
        IReadOnlyList<UserNotification> notifications,
        IReadOnlyDictionary<int, User> recipientsById,
        CancellationToken cancellationToken = default)
    {
        if (mode == NotificationChannelMode.Neither || notifications.Count == 0)
        {
            return;
        }

        if (mode is NotificationChannelMode.Email or NotificationChannelMode.Both)
        {
            await TrySendEmailAsync(notifications, recipientsById, cancellationToken);
        }

        if (mode is NotificationChannelMode.Teams or NotificationChannelMode.Both)
        {
            await TrySendTeamsAsync(notifications, recipientsById, cancellationToken);
        }
    }

    private async Task TrySendEmailAsync(
        IReadOnlyList<UserNotification> notifications,
        IReadOnlyDictionary<int, User> recipientsById,
        CancellationToken cancellationToken)
    {
        if (!IsEmailConfigured())
        {
            _logger.LogDebug(
                "Skipping email notifications because SMTP delivery is not configured.");
            return;
        }

        var notificationsByUser = notifications
            .GroupBy(notification => notification.UserId)
            .Where(group => recipientsById.ContainsKey(group.Key))
            .ToList();

        if (notificationsByUser.Count == 0)
        {
            return;
        }

        using var smtpClient = CreateSmtpClient();

        foreach (var group in notificationsByUser)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var recipient = recipientsById[group.Key];
            if (string.IsNullOrWhiteSpace(recipient.Email))
            {
                continue;
            }

            var orderedNotifications = group
                .OrderByDescending(notification => notification.CreatedDateUtc)
                .ToList();
            var subject = orderedNotifications.Count == 1
                ? orderedNotifications[0].Title
                : $"CORTEX notifications ({orderedNotifications.Count})";

            using var message = new MailMessage
            {
                From = new MailAddress(
                    _emailOptions.FromAddress,
                    string.IsNullOrWhiteSpace(_emailOptions.FromDisplayName)
                        ? "CORTEX"
                        : _emailOptions.FromDisplayName),
                Subject = subject,
                Body = BuildEmailBody(recipient, orderedNotifications),
                IsBodyHtml = false
            };

            message.To.Add(recipient.Email);

            try
            {
                await smtpClient.SendMailAsync(message, cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to send email notification to {RecipientEmail}.",
                    recipient.Email);
            }
        }
    }

    private async Task TrySendTeamsAsync(
        IReadOnlyList<UserNotification> notifications,
        IReadOnlyDictionary<int, User> recipientsById,
        CancellationToken cancellationToken)
    {
        if (!IsTeamsConfigured())
        {
            _logger.LogDebug(
                "Skipping Teams notifications because no webhook URL is configured.");
            return;
        }

        var payload = new Dictionary<string, object?>
        {
            ["@type"] = "MessageCard",
            ["@context"] = "https://schema.org/extensions",
            ["summary"] = BuildTeamsSummary(notifications),
            ["themeColor"] = ResolveTeamsThemeColor(notifications),
            ["title"] = BuildTeamsSummary(notifications),
            ["text"] = BuildTeamsMessage(notifications, recipientsById)
        };

        try
        {
            using var response = await _httpClient.PostAsync(
                _teamsOptions.WebhookUrl,
                new StringContent(
                    JsonSerializer.Serialize(payload),
                    Encoding.UTF8,
                    "application/json"),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Teams notification webhook returned {StatusCode}.",
                    response.StatusCode);
            }
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to post Teams notification.");
        }
    }

    private bool IsEmailConfigured()
    {
        return !string.IsNullOrWhiteSpace(_emailOptions.SmtpHost) &&
               _emailOptions.Port > 0 &&
               !string.IsNullOrWhiteSpace(_emailOptions.FromAddress);
    }

    private bool IsTeamsConfigured()
    {
        return !string.IsNullOrWhiteSpace(_teamsOptions.WebhookUrl);
    }

    private SmtpClient CreateSmtpClient()
    {
        var smtpClient = new SmtpClient(_emailOptions.SmtpHost, _emailOptions.Port)
        {
            EnableSsl = _emailOptions.UseSsl
        };

        if (!string.IsNullOrWhiteSpace(_emailOptions.UserName))
        {
            smtpClient.Credentials = new NetworkCredential(
                _emailOptions.UserName,
                _emailOptions.Password);
        }
        else
        {
            smtpClient.UseDefaultCredentials = true;
        }

        return smtpClient;
    }

    private static string BuildEmailBody(
        User recipient,
        IReadOnlyList<UserNotification> notifications)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Hello {ResolveRecipientName(recipient)},");
        builder.AppendLine();
        builder.AppendLine("CORTEX generated the following operational notification(s):");
        builder.AppendLine();

        foreach (var notification in notifications)
        {
            builder.AppendLine($"- {notification.Title}");
            builder.AppendLine($"  {notification.Message}");
            if (!string.IsNullOrWhiteSpace(notification.TicketId))
            {
                builder.AppendLine($"  Ticket: {notification.TicketId}");
            }

            builder.AppendLine(
                $"  Time (UTC): {notification.CreatedDateUtc:yyyy-MM-dd HH:mm:ss}");
            builder.AppendLine();
        }

        builder.AppendLine("You can review the latest updates in CORTEX.");
        return builder.ToString().TrimEnd();
    }

    private static string BuildTeamsSummary(IReadOnlyList<UserNotification> notifications)
    {
        return notifications.Count == 1
            ? notifications[0].Title
            : $"CORTEX notifications ({notifications.Count})";
    }

    private static string BuildTeamsMessage(
        IReadOnlyList<UserNotification> notifications,
        IReadOnlyDictionary<int, User> recipientsById)
    {
        var uniqueRecipients = notifications
            .Select(notification => recipientsById.TryGetValue(notification.UserId, out var user)
                ? ResolveRecipientName(user)
                : $"User #{notification.UserId}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToList();

        var builder = new StringBuilder();
        if (uniqueRecipients.Count > 0)
        {
            builder.Append("Recipients: ");
            builder.Append(string.Join(", ", uniqueRecipients));
            builder.Append("<br/><br/>");
        }

        foreach (var notification in notifications
                     .GroupBy(notification => new
                     {
                         notification.Title,
                         notification.Message,
                         notification.TicketId,
                         notification.Severity
                     })
                     .Select(group => group.First()))
        {
            builder.Append("<strong>");
            builder.Append(WebUtility.HtmlEncode(notification.Title));
            builder.Append("</strong><br/>");
            builder.Append(WebUtility.HtmlEncode(notification.Message));

            if (!string.IsNullOrWhiteSpace(notification.TicketId))
            {
                builder.Append("<br/>Ticket: ");
                builder.Append(WebUtility.HtmlEncode(notification.TicketId));
            }

            builder.Append("<br/><br/>");
        }

        return builder.ToString().TrimEnd();
    }

    private static string ResolveRecipientName(User recipient)
    {
        return !string.IsNullOrWhiteSpace(recipient.DisplayName)
            ? recipient.DisplayName.Trim()
            : !string.IsNullOrWhiteSpace(recipient.NickName)
                ? recipient.NickName.Trim()
                : recipient.Email;
    }

    private static string ResolveTeamsThemeColor(IReadOnlyList<UserNotification> notifications)
    {
        if (notifications.Any(notification => string.Equals(
                notification.Severity,
                "critical",
                StringComparison.OrdinalIgnoreCase)))
        {
            return "D13438";
        }

        if (notifications.Any(notification => string.Equals(
                notification.Severity,
                "warning",
                StringComparison.OrdinalIgnoreCase)))
        {
            return "FFB900";
        }

        return "0078D4";
    }
}
