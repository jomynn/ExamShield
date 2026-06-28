using ExamShield.Application.Commands.RegisterCapture;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands;

public sealed class RegisterCaptureAfterDeadlineTests
{
    private readonly ICaptureRepository           _captures   = Substitute.For<ICaptureRepository>();
    private readonly IDeviceRepository            _devices    = Substitute.For<IDeviceRepository>();
    private readonly ISignatureVerificationService _sigService = Substitute.For<ISignatureVerificationService>();
    private readonly IAuditLogRepository          _auditLog   = Substitute.For<IAuditLogRepository>();
    private readonly IExamRepository              _exams      = Substitute.For<IExamRepository>();
    private readonly IExamCandidateRepository     _candidates = Substitute.For<IExamCandidateRepository>();
    private readonly RegisterCaptureCommandHandler _sut;

    public RegisterCaptureAfterDeadlineTests()
    {
        _candidates.ExistsAsync(Arg.Any<ExamId>(), Arg.Any<StudentId>(), Arg.Any<CancellationToken>()).Returns(true);
        _sut = new RegisterCaptureCommandHandler(_captures, _devices, _sigService, _auditLog, _exams, _candidates);
    }

    private static Exam MakeExpiredExam()
    {
        var exam = Exam.Create("Expired Exam", null, 10,
            scheduledAt: DateTimeOffset.UtcNow.AddHours(-3),
            endsAt: DateTimeOffset.UtcNow.AddHours(-1));
        exam.Activate();
        return exam;
    }

    private static Device MakeApprovedDevice()
    {
        var device = Device.Register("Test Device", new PublicKey(new byte[32]));
        device.Approve();
        return device;
    }

    [Fact]
    public async Task Handle_WhenExamEndsAtPassed_ThrowsExamExpiredException()
    {
        var exam   = MakeExpiredExam();
        var device = MakeApprovedDevice();

        _exams.GetByIdAsync(Arg.Any<ExamId>(), default).Returns(exam);
        _devices.GetByIdAsync(Arg.Any<DeviceId>(), default).Returns(device);
        _sigService.Verify(Arg.Any<Hash>(), Arg.Any<Signature>(), Arg.Any<PublicKey>()).Returns(true);

        await Assert.ThrowsAsync<ExamExpiredException>(() =>
            _sut.Handle(new RegisterCaptureCommand(
                exam.Id.Value, Guid.NewGuid(), device.Id.Value,
                1, "a" + new string('0', 63), new byte[32]), default));
    }

    [Fact]
    public async Task Handle_WhenEndsAtIsNull_DoesNotThrowExamExpiredException()
    {
        var exam = Exam.Create("No-Deadline Exam", null, 10);
        exam.Activate();
        var device = MakeApprovedDevice();

        _exams.GetByIdAsync(Arg.Any<ExamId>(), default).Returns(exam);
        _devices.GetByIdAsync(Arg.Any<DeviceId>(), default).Returns(device);
        _sigService.Verify(Arg.Any<Hash>(), Arg.Any<Signature>(), Arg.Any<PublicKey>()).Returns(true);
        _captures.ExistsByStudentExamPageAsync(
            Arg.Any<StudentId>(), Arg.Any<ExamId>(), Arg.Any<PageNumber>(), default).Returns(false);

        var ex = await Record.ExceptionAsync(() =>
            _sut.Handle(new RegisterCaptureCommand(
                exam.Id.Value, Guid.NewGuid(), device.Id.Value,
                1, "a" + new string('0', 63), new byte[32]), default));

        Assert.IsNotType<ExamExpiredException>(ex);
    }
}
