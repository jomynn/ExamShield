using ExamShield.Application.Commands.RegisterCapture;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.RegisterCapture;

public sealed class RegisterCapturePendingDeviceTests
{
    private readonly ICaptureRepository           _captures   = Substitute.For<ICaptureRepository>();
    private readonly IDeviceRepository            _devices    = Substitute.For<IDeviceRepository>();
    private readonly ISignatureVerificationService _sig       = Substitute.For<ISignatureVerificationService>();
    private readonly IAuditLogRepository          _audit      = Substitute.For<IAuditLogRepository>();
    private readonly IExamRepository              _exams      = Substitute.For<IExamRepository>();
    private readonly IExamCandidateRepository     _candidates = Substitute.For<IExamCandidateRepository>();
    private readonly RegisterCaptureCommandHandler _sut;

    public RegisterCapturePendingDeviceTests()
    {
        var exam = Exam.Create("Exam", null, 10);
        exam.Activate();
        _exams.GetByIdAsync(Arg.Any<ExamId>(), Arg.Any<CancellationToken>()).Returns(exam);
        _sig.Verify(Arg.Any<Hash>(), Arg.Any<Signature>(), Arg.Any<PublicKey>()).Returns(true);
        _candidates.ExistsAsync(Arg.Any<ExamId>(), Arg.Any<StudentId>(), Arg.Any<CancellationToken>()).Returns(true);
        _sut = new RegisterCaptureCommandHandler(_captures, _devices, _sig, _audit, _exams, _candidates);
    }

    [Fact]
    public async Task Handle_PendingDevice_ThrowsDeviceNotApprovedException()
    {
        var device = Device.Register("Scanner", new PublicKey(new byte[] { 0x04 }));
        // NOT calling device.Approve() — remains Pending
        Assert.Equal(DeviceStatus.Pending, device.Status);
        _devices.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>()).Returns(device);

        var cmd = new RegisterCaptureCommand(
            Guid.NewGuid(), Guid.NewGuid(), device.Id.Value, 1,
            new string('a', 64), new byte[64]);

        await Assert.ThrowsAsync<DeviceNotApprovedException>(
            () => _sut.Handle(cmd, default));
    }

    [Fact]
    public async Task Handle_DisabledDevice_ThrowsDeviceNotApprovedException()
    {
        var device = Device.Register("Scanner", new PublicKey(new byte[] { 0x04 }));
        device.Approve();
        device.Disable(); // Approved → Disabled
        Assert.Equal(DeviceStatus.Disabled, device.Status);
        _devices.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>()).Returns(device);

        var cmd = new RegisterCaptureCommand(
            Guid.NewGuid(), Guid.NewGuid(), device.Id.Value, 1,
            new string('a', 64), new byte[64]);

        await Assert.ThrowsAsync<DeviceNotApprovedException>(
            () => _sut.Handle(cmd, default));
    }

    [Fact]
    public async Task Handle_ApprovedDevice_Succeeds()
    {
        var device = Device.Register("Scanner", new PublicKey(new byte[] { 0x04 }));
        device.Approve();
        Assert.Equal(DeviceStatus.Approved, device.Status);
        _devices.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>()).Returns(device);

        var hashHex = new string('b', 64);
        var cmd = new RegisterCaptureCommand(
            Guid.NewGuid(), Guid.NewGuid(), device.Id.Value, 1,
            hashHex, new byte[64]);

        var result = await _sut.Handle(cmd, default);

        Assert.NotEqual(Guid.Empty, result.CaptureId);
    }
}
