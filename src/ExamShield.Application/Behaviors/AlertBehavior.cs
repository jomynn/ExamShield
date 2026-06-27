using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using MediatR;

namespace ExamShield.Application.Behaviors;

public sealed class AlertBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest<TResponse>
{
    private readonly IAlertService _alertService;
    private readonly IRealtimeNotificationService _realtime;

    public AlertBehavior(IAlertService alertService, IRealtimeNotificationService realtime)
    {
        _alertService = alertService;
        _realtime = realtime;
    }

    public async Task<TResponse> Handle(
        TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        try
        {
            return await next(ct);
        }
        catch (HashMismatchException ex)
        {
            await _alertService.SendAsync(AlertType.HashMismatch, ex.Message, ct);
            await BroadcastSecurityAlertAsync(ex.Message, NotificationSeverity.Critical, ct);
            throw;
        }
        catch (InvalidSignatureException ex)
        {
            await _alertService.SendAsync(AlertType.InvalidSignature, ex.Message, ct);
            await BroadcastSecurityAlertAsync(ex.Message, NotificationSeverity.High, ct);
            throw;
        }
        catch (DuplicateUploadException ex)
        {
            await _alertService.SendAsync(AlertType.DuplicateUpload, ex.Message, ct);
            await BroadcastSecurityAlertAsync(ex.Message, NotificationSeverity.High, ct);
            throw;
        }
    }

    private Task BroadcastSecurityAlertAsync(string message, NotificationSeverity severity, CancellationToken ct) =>
        _realtime.BroadcastAsync(
            new RealtimeNotification(NotificationType.SecurityAlert, message, severity, DateTimeOffset.UtcNow), ct);
}
