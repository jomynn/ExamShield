using ExamShield.Application.Commands.DisableDevice;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.DisableDevice;

public sealed class DisableDeviceAuditTests
{
    private readonly IDeviceRepository   _devices  = Substitute.For<IDeviceRepository>();
    private readonly IAuditLogRepository _auditLog = Substitute.For<IAuditLogRepository>();
    private readonly DisableDeviceCommandHandler _sut;

    public DisableDeviceAuditTests() =>
        _sut = new DisableDeviceCommandHandler(_devices, _auditLog);

    [Fact]
    public async Task Handle_Disable_AppendsDeviceDisabledAuditEntry()
    {
        var device = Device.Register("Scanner-1", new PublicKey(new byte[32]));
        device.Approve();
        _devices.GetByIdAsync(Arg.Any<DeviceId>(), default).Returns(device);

        await _sut.Handle(new DisableDeviceCommand(Guid.NewGuid()), default);

        await _auditLog.Received(1).AppendAsync(
            Arg.Is<AuditLog>(a => a.Action == AuditAction.DeviceDisabled), default);
    }
}
