using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.DeviceHeartbeat;

public sealed class DeviceHeartbeatCommandHandler(IDeviceRepository devices)
    : IRequestHandler<DeviceHeartbeatCommand, DeviceHeartbeatResult>
{
    public async Task<DeviceHeartbeatResult> Handle(DeviceHeartbeatCommand command, CancellationToken ct)
    {
        var device = await devices.GetByIdAsync(new DeviceId(command.DeviceId), ct)
            ?? throw new DeviceNotFoundException(command.DeviceId);

        device.RecordHeartbeat();
        await devices.UpdateAsync(device, ct);

        return new DeviceHeartbeatResult(device.Id.Value, device.LastSeenAt!.Value);
    }
}
