using ExamShield.Application.Commands.ApproveDevice;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.ApproveDevice;

public sealed class ApproveDeviceCommandHandlerTests
{
    private readonly IDeviceRepository _devices = Substitute.For<IDeviceRepository>();
    private readonly ApproveDeviceCommandHandler _sut;

    public ApproveDeviceCommandHandlerTests() => _sut = new ApproveDeviceCommandHandler(_devices);

    [Fact]
    public async Task Handle_PendingDevice_SetsStatusApproved()
    {
        var device = Device.Register("Scanner", new PublicKey(new byte[] { 0x04 }));
        _devices.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>()).Returns(device);

        await _sut.Handle(new ApproveDeviceCommand(device.Id.Value), default);

        Assert.Equal(DeviceStatus.Approved, device.Status);
        Assert.True(device.IsActive);
    }

    [Fact]
    public async Task Handle_PendingDevice_PersistsUpdate()
    {
        var device = Device.Register("Scanner", new PublicKey(new byte[] { 0x04 }));
        _devices.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>()).Returns(device);

        await _sut.Handle(new ApproveDeviceCommand(device.Id.Value), default);

        await _devices.Received(1).UpdateAsync(device, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_UnknownDevice_ThrowsDeviceNotFoundException()
    {
        _devices.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>())
            .Returns((Device?)null);

        await Assert.ThrowsAsync<DeviceNotFoundException>(
            () => _sut.Handle(new ApproveDeviceCommand(Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Handle_RegisterCapture_RejectsDeviceBeforeApproval()
    {
        var device = Device.Register("Scanner", new PublicKey(new byte[] { 0x04 }));

        Assert.Equal(DeviceStatus.Pending, device.Status);
        Assert.False(device.IsActive);

        device.Approve();

        Assert.Equal(DeviceStatus.Approved, device.Status);
        Assert.True(device.IsActive);
    }
}
