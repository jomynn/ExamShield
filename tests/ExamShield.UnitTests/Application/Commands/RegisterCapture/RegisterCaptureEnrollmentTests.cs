using ExamShield.Application.Commands.RegisterCapture;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.Services;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.RegisterCapture;

public sealed class RegisterCaptureEnrollmentTests
{
    private readonly ICaptureRepository           _captures   = Substitute.For<ICaptureRepository>();
    private readonly IDeviceRepository            _devices    = Substitute.For<IDeviceRepository>();
    private readonly ISignatureVerificationService _sigService = Substitute.For<ISignatureVerificationService>();
    private readonly IAuditLogRepository          _auditLog   = Substitute.For<IAuditLogRepository>();
    private readonly IExamRepository              _exams      = Substitute.For<IExamRepository>();
    private readonly IExamCandidateRepository     _candidates = Substitute.For<IExamCandidateRepository>();
    private readonly RegisterCaptureCommandHandler _sut;

    private static readonly byte[] AnyHash = new byte[32];
    private static readonly byte[] AnySig  = new byte[64];

    public RegisterCaptureEnrollmentTests()
    {
        _sut = new RegisterCaptureCommandHandler(
            _captures, _devices, _sigService, _auditLog, _exams, _candidates);

        var exam = Exam.Create("Test Exam", null, 10);
        exam.Activate();
        _exams.GetByIdAsync(Arg.Any<ExamId>(), default).Returns(exam);

        var device = Device.Register("Scanner", new PublicKey(new byte[32]));
        device.Approve();
        _devices.GetByIdAsync(Arg.Any<DeviceId>(), default).Returns(device);

        _sigService.Verify(Arg.Any<Hash>(), Arg.Any<Signature>(), Arg.Any<PublicKey>()).Returns(true);
    }

    [Fact]
    public async Task Handle_UnenrolledStudent_ThrowsStudentNotEnrolledException()
    {
        _candidates.ExistsAsync(Arg.Any<ExamId>(), Arg.Any<StudentId>(), default).Returns(false);

        var act = () => _sut.Handle(new RegisterCaptureCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
            Convert.ToHexString(AnyHash), AnySig), default);

        await act.Should().ThrowAsync<StudentNotEnrolledException>();
    }

    [Fact]
    public async Task Handle_UnenrolledStudent_NeverPersistsCapture()
    {
        _candidates.ExistsAsync(Arg.Any<ExamId>(), Arg.Any<StudentId>(), default).Returns(false);

        try
        {
            await _sut.Handle(new RegisterCaptureCommand(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
                Convert.ToHexString(AnyHash), AnySig), default);
        }
        catch { /* expected */ }

        await _captures.DidNotReceive().AddAsync(Arg.Any<Capture>(), default);
    }

    [Fact]
    public async Task Handle_EnrolledStudent_RegistersCapture()
    {
        _candidates.ExistsAsync(Arg.Any<ExamId>(), Arg.Any<StudentId>(), default).Returns(true);

        var result = await _sut.Handle(new RegisterCaptureCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
            Convert.ToHexString(AnyHash), AnySig), default);

        result.CaptureId.Should().NotBeEmpty();
        await _captures.Received(1).AddAsync(Arg.Any<Capture>(), default);
    }
}
