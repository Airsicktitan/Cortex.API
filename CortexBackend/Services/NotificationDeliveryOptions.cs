namespace Cortex.API.Services;

public class EmailNotificationOptions
{
    public string SmtpHost { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromDisplayName { get; set; } = "CORTEX";
    public bool UseSsl { get; set; } = true;
}

public class TeamsNotificationOptions
{
    public string WebhookUrl { get; set; } = string.Empty;
}
