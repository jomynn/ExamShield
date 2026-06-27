using ExamShield.Domain.Enums;

namespace ExamShield.Domain.Interfaces;

public enum NotificationType
{
    SecurityAlert,
    CaptureRegistered,
    OcrCompleted,
    ScorePublished,
    ReviewRequired,
    SystemInfo
}

public enum NotificationSeverity { Info, Warning, High, Critical }

public sealed record RealtimeNotification(
    NotificationType Type,
    string Message,
    NotificationSeverity Severity,
    DateTimeOffset OccurredAt);

public interface IRealtimeNotificationService
{
    Task BroadcastAsync(RealtimeNotification notification, CancellationToken ct = default);
}
