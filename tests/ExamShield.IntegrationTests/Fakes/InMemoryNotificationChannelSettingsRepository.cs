using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;

namespace ExamShield.IntegrationTests.Fakes;

public sealed class InMemoryNotificationChannelSettingsRepository : INotificationChannelSettingsRepository
{
    private NotificationChannelSettings _settings = NotificationChannelSettings.CreateDefault();

    public Task<NotificationChannelSettings> GetAsync(CancellationToken ct = default) =>
        Task.FromResult(_settings);

    public Task SaveAsync(NotificationChannelSettings settings, CancellationToken ct = default)
    {
        _settings = settings;
        return Task.CompletedTask;
    }
}
