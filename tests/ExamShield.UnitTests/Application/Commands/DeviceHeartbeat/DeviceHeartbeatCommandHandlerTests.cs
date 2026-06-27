using ExamShield.Application.Commands.DeviceHeartbeat;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.DeviceHeartbeat;

public sealed class DeviceHeartbeatCommandHandlerTests
{
    private readonly IDeviceRepository _devices = Substitute.For<IDeviceRepository>();
    private readonly DeviceHeartbeatCommandHandler _sut;

    public DeviceHeartbeatCommandHandlerTests()
    {
        _sut = new DeviceHeartbeatCommandHandler(_devices);
    }

    [Fact]
    public async Task Handle_WhenDeviceExists_UpdatesLastSeenAt()
    {
        using var ecdsa = System.Security.Cryptography.ECDsa.Create(
            System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var device = Device.Register("Test Device", new PublicKey(ecdsa.ExportSubjectPublicKeyInfo()));
        device.Approve();
        _devices.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>())
            .Returns(device);

        var command = new DeviceHeartbeatCommand(device.Id.Value);
        await _sut.Handle(command, default);

        Assert.NotNull(device.LastSeenAt);
    }

    [Fact]
    public async Task Handle_WhenDeviceNotFound_ThrowsDeviceNotFoundException()
    {
        _devices.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>())
            .Returns((Device?)null);

        await Assert.ThrowsAsync<DeviceNotFoundException>(
            () => _sut.Handle(new DeviceHeartbeatCommand(Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Handle_WhenDeviceInactive_ThrowsInvalidOperationException()
    {
        using var ecdsa = System.Security.Cryptography.ECDsa.Create(
            System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var device = Device.Register("Disabled Device", new PublicKey(ecdsa.ExportSubjectPublicKeyInfo()));
        device.Disable();
        _devices.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>())
            .Returns(device);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.Handle(new DeviceHeartbeatCommand(device.Id.Value), default));
    }

    [Fact]
    public async Task Handle_WhenDeviceExists_PersistsUpdate()
    {
        using var ecdsa = System.Security.Cryptography.ECDsa.Create(
            System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var device = Device.Register("Test Device", new PublicKey(ecdsa.ExportSubjectPublicKeyInfo()));
        device.Approve();
        _devices.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>())
            .Returns(device);

        await _sut.Handle(new DeviceHeartbeatCommand(device.Id.Value), default);

        await _devices.Received(1).UpdateAsync(device, Arg.Any<CancellationToken>());
    }
}
