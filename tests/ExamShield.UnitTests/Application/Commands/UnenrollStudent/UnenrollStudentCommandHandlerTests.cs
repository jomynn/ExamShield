using ExamShield.Application.Commands.UnenrollStudent;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.UnenrollStudent;

public sealed class UnenrollStudentCommandHandlerTests
{
    private readonly IExamCandidateRepository _candidates = Substitute.For<IExamCandidateRepository>();
    private readonly ICaptureRepository       _captures   = Substitute.For<ICaptureRepository>();
    private readonly IExamRepository          _exams      = Substitute.For<IExamRepository>();
    private readonly IAuditLogRepository      _audit      = Substitute.For<IAuditLogRepository>();
    private readonly UnenrollStudentCommandHandler _sut;

    private static readonly Guid ExamId    = Guid.NewGuid();
    private static readonly Guid StudentId = Guid.NewGuid();

    public UnenrollStudentCommandHandlerTests()
    {
        var exam = Exam.Create("Test", null, 5);
        _exams.GetByIdAsync(Arg.Any<ExamId>(), Arg.Any<CancellationToken>()).Returns(exam);

        _candidates.ExistsAsync(
            Arg.Any<ExamId>(), Arg.Any<StudentId>(), Arg.Any<CancellationToken>())
            .Returns(true);

        _candidates.ListByExamIdAsync(Arg.Any<ExamId>(), Arg.Any<CancellationToken>())
            .Returns(new List<ExamCandidate>());

        _captures.ExistsByStudentExamPageAsync(
            Arg.Any<StudentId>(), Arg.Any<ExamId>(), Arg.Any<PageNumber>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _captures.ListByStudentIdAsync(Arg.Any<StudentId>(), Arg.Any<CancellationToken>())
            .Returns(new List<Capture>());

        _sut = new UnenrollStudentCommandHandler(_candidates, _captures, _exams, _audit);
    }

    private UnenrollStudentCommand Cmd() => new(ExamId, StudentId);

    [Fact]
    public async Task Handle_ValidUnenrollment_RemovesCandidate()
    {
        await _sut.Handle(Cmd(), default);

        await _candidates.Received(1).RemoveAsync(
            Arg.Is<ExamId>(e => e.Value == ExamId),
            Arg.Is<StudentId>(s => s.Value == StudentId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_StudentNotEnrolled_ThrowsStudentNotEnrolledException()
    {
        _candidates.ExistsAsync(Arg.Any<ExamId>(), Arg.Any<StudentId>(), Arg.Any<CancellationToken>())
            .Returns(false);

        await Assert.ThrowsAsync<StudentNotEnrolledException>(() => _sut.Handle(Cmd(), default));
    }

    [Fact]
    public async Task Handle_ExamNotFound_ThrowsKeyNotFoundException()
    {
        _exams.GetByIdAsync(Arg.Any<ExamId>(), Arg.Any<CancellationToken>())
            .Returns((Exam?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.Handle(Cmd(), default));
    }

    [Fact]
    public async Task Handle_StudentHasCapture_ThrowsStudentHasCaptureException()
    {
        var hash      = Hash.FromBytes(new byte[32]);
        var signature = new Signature(new byte[64]);
        var capture   = Capture.Create(
            new ExamId(ExamId), new StudentId(StudentId),
            DeviceId.New(), new PageNumber(1), hash, signature);

        _captures.ListByStudentIdAsync(new StudentId(StudentId), Arg.Any<CancellationToken>())
            .Returns(new[] { capture });

        await Assert.ThrowsAsync<StudentHasCaptureException>(() => _sut.Handle(Cmd(), default));
    }

    [Fact]
    public async Task Handle_ValidUnenrollment_AppendsAuditLog()
    {
        await _sut.Handle(Cmd(), default);

        await _audit.Received(1).AppendAsync(
            Arg.Is<AuditLog>(a => a.Action == AuditAction.StudentUnenrolled),
            Arg.Any<CancellationToken>());
    }
}
