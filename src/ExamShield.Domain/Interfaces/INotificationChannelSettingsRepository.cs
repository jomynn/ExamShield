using ExamShield.Domain.Entities;

namespace ExamShield.Domain.Interfaces;

public interface INotificationChannelSettingsRepository
{
    Task<NotificationChannelSettings> GetAsync(CancellationToken ct = default);
    Task SaveAsync(NotificationChannelSettings settings, CancellationToken ct = default);
}
