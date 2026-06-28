using ExamShield.Application.Queries.GetSettings;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using MediatR;

namespace ExamShield.Application.Commands.UpdateSettings;

public sealed class UpdateSettingsCommandHandler(
    ISystemSettingsRepository repository,
    IAuditLogRepository auditLog)
    : IRequestHandler<UpdateSettingsCommand, SettingsDto>
{
    public async Task<SettingsDto> Handle(UpdateSettingsCommand request, CancellationToken ct)
    {
        var settings = await repository.GetAsync(ct);
        settings.Update(
            request.OcrConfidenceThreshold,
            request.NotificationsEnabled,
            request.NotificationSeverity,
            request.AccessTokenExpiryMinutes,
            request.RefreshTokenExpiryDays);
        await repository.SaveAsync(settings, ct);
        await auditLog.AppendAsync(AuditLog.Record(AuditAction.SettingsUpdated), ct);
        return new SettingsDto(
            settings.OcrConfidenceThreshold, settings.NotificationsEnabled,
            settings.NotificationSeverity, settings.AccessTokenExpiryMinutes,
            settings.RefreshTokenExpiryDays);
    }
}
