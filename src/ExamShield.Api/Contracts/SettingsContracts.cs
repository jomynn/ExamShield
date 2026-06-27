namespace ExamShield.Api.Contracts;

public sealed record SettingsResponse(
    double OcrConfidenceThreshold,
    bool NotificationsEnabled,
    string NotificationSeverity,
    int AccessTokenExpiryMinutes,
    int RefreshTokenExpiryDays);

public sealed record UpdateSettingsRequest(
    double OcrConfidenceThreshold,
    bool NotificationsEnabled,
    string NotificationSeverity,
    int AccessTokenExpiryMinutes,
    int RefreshTokenExpiryDays);

public sealed record AlertTestResponse(bool Sent, string? Error);

public sealed record NotificationChannelSettingsResponse(
    bool EmailEnabled,   string? EmailRecipients,
    bool SlackEnabled,   string? SlackWebhookUrl,
    bool LineEnabled,    string? LineNotifyToken,
    bool WebhookEnabled, string? WebhookUrl,
    DateTimeOffset UpdatedAt);

public sealed record UpdateNotificationChannelSettingsRequest(
    bool EmailEnabled,   string? EmailRecipients,
    bool SlackEnabled,   string? SlackWebhookUrl,
    bool LineEnabled,    string? LineNotifyToken,
    bool WebhookEnabled, string? WebhookUrl);
