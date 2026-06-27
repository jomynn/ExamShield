using ExamShield.Application.Commands.RegisterDevice;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.RegisterDevice;

public sealed class RegisterDeviceDuplicateKeyTests
{
    private readonly IDeviceRepository   _devices  = Substitute.For<IDeviceRepository>();
    private readonly IAuditLogRepository _auditLog = Substitute.For<IAuditLogRepository>();
    private readonly RegisterDeviceCommandHandler _sut;

    public RegisterDeviceDuplicateKeyTests() =>
        _sut = new RegisterDeviceCommandHandler(_devices, _auditLog);

    [Fact]
    public async Task Handle_DuplicatePublicKey_ThrowsDuplicateDevicePublicKeyException()
    {
        var keyBytes = new byte[32];
        _devices.ExistsByPublicKeyAsync(Arg.Any<PublicKey>(), default).Returns(true);

        var act = () => _sut.Handle(new RegisterDeviceCommand("Device A", keyBytes), default);

        await act.Should().ThrowAsync<DuplicateDevicePublicKeyException>();
    }

    [Fact]
    public async Task Handle_DuplicatePublicKey_DoesNotPersistDevice()
    {
        var keyBytes = new byte[32];
        _devices.ExistsByPublicKeyAsync(Arg.Any<PublicKey>(), default).Returns(true);

        try { await _sut.Handle(new RegisterDeviceCommand("Device B", keyBytes), default); } catch { }

        await _devices.DidNotReceive().AddAsync(Arg.Any<Device>(), default);
    }

    [Fact]
    public async Task Handle_UniquePublicKey_Succeeds()
    {
        var keyBytes = new byte[32];
        _devices.ExistsByPublicKeyAsync(Arg.Any<PublicKey>(), default).Returns(false);

        var act = () => _sut.Handle(new RegisterDeviceCommand("Device C", keyBytes), default);

        await act.Should().NotThrowAsync();
        await _devices.Received(1).AddAsync(Arg.Any<Device>(), default);
    }
}
