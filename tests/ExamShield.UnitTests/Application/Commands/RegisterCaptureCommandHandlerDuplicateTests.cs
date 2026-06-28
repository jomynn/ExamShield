using ExamShield.Application.Commands.RegisterCapture;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands;

public sealed class RegisterCaptureCommandHandlerDuplicateTests
{
    private readonly ICaptureRepository             _repo       = Substitute.For<ICaptureRepository>();
    private readonly IDeviceRepository              _devices    = Substitute.For<IDeviceRepository>();
    private readonly ISignatureVerificationService  _sig        = Substitute.For<ISignatureVerificationService>();
    private readonly IAuditLogRepository            _audit      = Substitute.For<IAuditLogRepository>();
    private readonly IExamRepository                _exams      = Substitute.For<IExamRepository>();
    private readonly IExamCandidateRepository       _candidates = Substitute.For<IExamCandidateRepository>();
    private readonly RegisterCaptureCommandHandler  _sut;

    private static readonly Guid ExamId    = Guid.NewGuid();
    private static readonly Guid StudentId = Guid.NewGuid();
    private static readonly Guid DeviceId  = Guid.NewGuid();
    private static readonly string HashHex = new string('a', 64);

    public RegisterCaptureCommandHandlerDuplicateTests()
    {
        var exam = Exam.Create("Test", null, 5);
        exam.Activate();
        _exams.GetByIdAsync(Arg.Any<ExamId>(), Arg.Any<CancellationToken>()).Returns(exam);

        var device = Device.Register("Dev", new PublicKey(new byte[91]));
        device.Approve();
        _devices.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>()).Returns(device);

        _sig.Verify(Arg.Any<Hash>(), Arg.Any<Signature>(), Arg.Any<PublicKey>()).Returns(true);
        _candidates.ExistsAsync(Arg.Any<ExamId>(), Arg.Any<StudentId>(), Arg.Any<CancellationToken>()).Returns(true);

        _repo.ExistsByStudentExamPageAsync(
            Arg.Any<StudentId>(), Arg.Any<ExamId>(), Arg.Any<PageNumber>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _sut = new RegisterCaptureCommandHandler(_repo, _devices, _sig, _audit, _exams, _candidates);
    }

    private RegisterCaptureCommand MakeCommand(int page = 1) =>
        new(ExamId, StudentId, DeviceId, page, HashHex, new byte[64]);

    [Fact]
    public async Task Handle_NoPriorCapture_Succeeds()
    {
        var result = await _sut.Handle(MakeCommand(), default);

        Assert.NotEqual(Guid.Empty, result.CaptureId);
    }

    [Fact]
    public async Task Handle_ExistingActiveCapture_ThrowsDuplicateCaptureException()
    {
        _repo.ExistsByStudentExamPageAsync(
            Arg.Any<StudentId>(), Arg.Any<ExamId>(), Arg.Any<PageNumber>(), Arg.Any<CancellationToken>())
            .Returns(true);

        await Assert.ThrowsAsync<DuplicateCaptureException>(
            () => _sut.Handle(MakeCommand(), default));
    }

    [Fact]
    public async Task Handle_DuplicateCapture_DoesNotPersist()
    {
        _repo.ExistsByStudentExamPageAsync(
            Arg.Any<StudentId>(), Arg.Any<ExamId>(), Arg.Any<PageNumber>(), Arg.Any<CancellationToken>())
            .Returns(true);

        try { await _sut.Handle(MakeCommand(), default); } catch (DuplicateCaptureException) { }

        await _repo.DidNotReceive().AddAsync(Arg.Any<Capture>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_DifferentPageSameStudent_Succeeds()
    {
        _repo.ExistsByStudentExamPageAsync(
                Arg.Any<StudentId>(), Arg.Any<ExamId>(), Arg.Is<PageNumber>(p => p.Value == 1),
                Arg.Any<CancellationToken>())
            .Returns(true);
        _repo.ExistsByStudentExamPageAsync(
                Arg.Any<StudentId>(), Arg.Any<ExamId>(), Arg.Is<PageNumber>(p => p.Value == 2),
                Arg.Any<CancellationToken>())
            .Returns(false);

        var result = await _sut.Handle(MakeCommand(page: 2), default);

        Assert.NotEqual(Guid.Empty, result.CaptureId);
    }

    [Fact]
    public async Task Handle_DuplicateCapture_ChecksCorrectStudentExamPage()
    {
        _repo.ExistsByStudentExamPageAsync(
            Arg.Any<StudentId>(), Arg.Any<ExamId>(), Arg.Any<PageNumber>(), Arg.Any<CancellationToken>())
            .Returns(false);

        await _sut.Handle(MakeCommand(page: 3), default);

        await _repo.Received(1).ExistsByStudentExamPageAsync(
            Arg.Is<StudentId>(s => s.Value == StudentId),
            Arg.Is<ExamId>(e => e.Value == ExamId),
            Arg.Is<PageNumber>(p => p.Value == 3),
            Arg.Any<CancellationToken>());
    }
}
