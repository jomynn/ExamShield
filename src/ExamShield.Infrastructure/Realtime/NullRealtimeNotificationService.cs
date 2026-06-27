using ExamShield.Domain.Interfaces;

namespace ExamShield.Infrastructure.Realtime;

public sealed class NullRealtimeNotificationService : IRealtimeNotificationService
{
    public Task BroadcastAsync(RealtimeNotification notification, CancellationToken ct = default) =>
        Task.CompletedTask;
}
