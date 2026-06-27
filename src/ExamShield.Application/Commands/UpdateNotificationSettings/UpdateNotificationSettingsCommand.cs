using ExamShield.Application.Queries.GetNotificationSettings;
using ExamShield.Domain.Interfaces;
using MediatR;

namespace ExamShield.Application.Commands.UpdateNotificationSettings;

public sealed record UpdateNotificationSettingsCommand(
    bool EmailEnabled,   string? EmailRecipients,
    bool SlackEnabled,   string? SlackWebhookUrl,
    bool LineEnabled,    string? LineNotifyToken,
    bool WebhookEnabled, string? WebhookUrl)
    : IRequest<NotificationSettingsDto>;

public sealed class UpdateNotificationSettingsCommandHandler(INotificationChannelSettingsRepository repo)
    : IRequestHandler<UpdateNotificationSettingsCommand, NotificationSettingsDto>
{
    public async Task<NotificationSettingsDto> Handle(UpdateNotificationSettingsCommand cmd, CancellationToken ct)
    {
        var settings = await repo.GetAsync(ct);
        settings.Update(
            cmd.EmailEnabled,   cmd.EmailRecipients,
            cmd.SlackEnabled,   cmd.SlackWebhookUrl,
            cmd.LineEnabled,    cmd.LineNotifyToken,
            cmd.WebhookEnabled, cmd.WebhookUrl);
        await repo.SaveAsync(settings, ct);
        return new NotificationSettingsDto(
            settings.EmailEnabled,   settings.EmailRecipients,
            settings.SlackEnabled,   settings.SlackWebhookUrl,
            settings.LineEnabled,    settings.LineNotifyToken,
            settings.WebhookEnabled, settings.WebhookUrl,
            settings.UpdatedAt);
    }
}
