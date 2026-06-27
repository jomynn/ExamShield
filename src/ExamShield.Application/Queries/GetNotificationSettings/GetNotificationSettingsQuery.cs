using ExamShield.Domain.Interfaces;
using MediatR;

namespace ExamShield.Application.Queries.GetNotificationSettings;

public sealed record NotificationSettingsDto(
    bool EmailEnabled,   string? EmailRecipients,
    bool SlackEnabled,   string? SlackWebhookUrl,
    bool LineEnabled,    string? LineNotifyToken,
    bool WebhookEnabled, string? WebhookUrl,
    DateTimeOffset UpdatedAt);

public sealed record GetNotificationSettingsQuery : IRequest<NotificationSettingsDto>;

public sealed class GetNotificationSettingsQueryHandler(INotificationChannelSettingsRepository repo)
    : IRequestHandler<GetNotificationSettingsQuery, NotificationSettingsDto>
{
    public async Task<NotificationSettingsDto> Handle(GetNotificationSettingsQuery _, CancellationToken ct)
    {
        var s = await repo.GetAsync(ct);
        return new NotificationSettingsDto(
            s.EmailEnabled,   s.EmailRecipients,
            s.SlackEnabled,   s.SlackWebhookUrl,
            s.LineEnabled,    s.LineNotifyToken,
            s.WebhookEnabled, s.WebhookUrl,
            s.UpdatedAt);
    }
}
