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
