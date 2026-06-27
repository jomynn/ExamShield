namespace ExamShield.Domain.Entities;

public sealed class NotificationChannelSettings
{
    public int Id { get; private set; } = 1;

    public bool EmailEnabled { get; private set; }
    public string? EmailRecipients { get; private set; }

    public bool SlackEnabled { get; private set; }
    public string? SlackWebhookUrl { get; private set; }

    public bool LineEnabled { get; private set; }
    public string? LineNotifyToken { get; private set; }

    public bool WebhookEnabled { get; private set; }
    public string? WebhookUrl { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    private NotificationChannelSettings() { }

    public static NotificationChannelSettings CreateDefault() => new();

    public void Update(
        bool emailEnabled,   string? emailRecipients,
        bool slackEnabled,   string? slackWebhookUrl,
        bool lineEnabled,    string? lineNotifyToken,
        bool webhookEnabled, string? webhookUrl)
    {
        if (emailEnabled && string.IsNullOrWhiteSpace(emailRecipients))
            throw new ArgumentException("Email recipients are required when email notifications are enabled.", nameof(emailRecipients));

        if (slackEnabled)
        {
            if (string.IsNullOrWhiteSpace(slackWebhookUrl))
                throw new ArgumentException("Slack webhook URL is required when Slack notifications are enabled.", nameof(slackWebhookUrl));
            if (!IsHttpUrl(slackWebhookUrl))
                throw new ArgumentException("Slack webhook URL must be a valid http/https URL.", nameof(slackWebhookUrl));
        }

        if (webhookEnabled)
        {
            if (string.IsNullOrWhiteSpace(webhookUrl))
                throw new ArgumentException("Webhook URL is required when webhook notifications are enabled.", nameof(webhookUrl));
            if (!IsHttpUrl(webhookUrl))
                throw new ArgumentException("Webhook URL must be a valid http/https URL.", nameof(webhookUrl));
        }

        EmailEnabled   = emailEnabled;   EmailRecipients  = emailRecipients;
        SlackEnabled   = slackEnabled;   SlackWebhookUrl  = slackWebhookUrl;
        LineEnabled    = lineEnabled;    LineNotifyToken  = lineNotifyToken;
        WebhookEnabled = webhookEnabled; WebhookUrl       = webhookUrl;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static bool IsHttpUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var u) &&
        (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);
}
