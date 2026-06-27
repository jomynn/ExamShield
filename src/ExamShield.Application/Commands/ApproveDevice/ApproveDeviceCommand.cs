using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.ApproveDevice;

public sealed record ApproveDeviceCommand(Guid DeviceId) : IRequest;

public sealed class ApproveDeviceCommandHandler(IDeviceRepository devices)
    : IRequestHandler<ApproveDeviceCommand>
{
    public async Task Handle(ApproveDeviceCommand command, CancellationToken ct)
    {
        var device = await devices.GetByIdAsync(new DeviceId(command.DeviceId), ct)
            ?? throw new DeviceNotFoundException(command.DeviceId);

        device.Approve();
        await devices.UpdateAsync(device, ct);
    }
}
