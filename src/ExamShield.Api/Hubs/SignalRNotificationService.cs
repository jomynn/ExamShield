using ExamShield.Domain.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace ExamShield.Api.Hubs;

public sealed class SignalRNotificationService(IHubContext<NotificationHub> hub)
    : IRealtimeNotificationService
{
    public Task BroadcastAsync(RealtimeNotification notification, CancellationToken ct = default) =>
        hub.Clients.All.SendAsync("Notification", new
        {
            type = notification.Type.ToString(),
            message = notification.Message,
            severity = notification.Severity.ToString(),
            occurredAt = notification.OccurredAt
        }, ct);
}
