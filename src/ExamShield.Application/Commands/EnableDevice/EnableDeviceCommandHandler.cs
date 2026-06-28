using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.EnableDevice;

public sealed class EnableDeviceCommandHandler(
    IDeviceRepository devices,
    IAuditLogRepository auditLog)
    : IRequestHandler<EnableDeviceCommand>
{
    public async Task Handle(EnableDeviceCommand request, CancellationToken ct)
    {
        var device = await devices.GetByIdAsync(new DeviceId(request.DeviceId), ct)
            ?? throw new DeviceNotFoundException(request.DeviceId);
        device.Enable();
        await devices.SaveAsync(device, ct);
        await auditLog.AppendAsync(AuditLog.Record(AuditAction.DeviceEnabled), ct);
    }
}
