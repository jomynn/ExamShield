using ExamShield.Application.Commands.EnableDevice;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.EnableDevice;

public sealed class EnableDeviceAuditTests
{
    private readonly IDeviceRepository   _devices  = Substitute.For<IDeviceRepository>();
    private readonly IAuditLogRepository _auditLog = Substitute.For<IAuditLogRepository>();
    private readonly EnableDeviceCommandHandler _sut;

    public EnableDeviceAuditTests() =>
        _sut = new EnableDeviceCommandHandler(_devices, _auditLog);

    [Fact]
    public async Task Handle_Enable_AppendsDeviceEnabledAuditEntry()
    {
        var device = Device.Register("Scanner-1", new PublicKey(new byte[32]));
        device.Disable();
        _devices.GetByIdAsync(Arg.Any<DeviceId>(), default).Returns(device);

        await _sut.Handle(new EnableDeviceCommand(Guid.NewGuid()), default);

        await _auditLog.Received(1).AppendAsync(
            Arg.Is<AuditLog>(a => a.Action == AuditAction.DeviceEnabled), default);
    }
}
