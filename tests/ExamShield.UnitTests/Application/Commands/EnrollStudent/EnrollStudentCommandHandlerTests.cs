using ExamShield.Application.Commands.EnrollStudent;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.EnrollStudent;

public sealed class EnrollStudentCommandHandlerTests
{
    private readonly IExamRepository            _exams      = Substitute.For<IExamRepository>();
    private readonly IExamCandidateRepository   _candidates = Substitute.For<IExamCandidateRepository>();
    private readonly IAuditLogRepository        _audit      = Substitute.For<IAuditLogRepository>();
    private readonly EnrollStudentCommandHandler _sut;

    private readonly Exam      _exam;
    private readonly StudentId _student = StudentId.New();

    public EnrollStudentCommandHandlerTests()
    {
        _exam = Exam.Create("Enrollment Test", null, 5);
        _exam.Activate();
        _exams.GetByIdAsync(Arg.Any<ExamId>(), Arg.Any<CancellationToken>()).Returns(_exam);
        _candidates.ExistsAsync(Arg.Any<ExamId>(), Arg.Any<StudentId>(), Arg.Any<CancellationToken>())
            .Returns(false);

        _sut = new EnrollStudentCommandHandler(_exams, _candidates, _audit);
    }

    [Fact]
    public async Task Handle_WithValidStudent_PersistsCandidate()
    {
        await _sut.Handle(new EnrollStudentCommand(_exam.Id.Value, _student.Value), default);

        await _candidates.Received(1).AddAsync(
            Arg.Is<ExamCandidate>(c => c.StudentId == _student && c.ExamId == _exam.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithValidStudent_AppendsAuditLog()
    {
        await _sut.Handle(new EnrollStudentCommand(_exam.Id.Value, _student.Value), default);

        await _audit.Received(1).AppendAsync(Arg.Any<AuditLog>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenExamNotFound_ThrowsKeyNotFoundException()
    {
        _exams.GetByIdAsync(Arg.Any<ExamId>(), Arg.Any<CancellationToken>())
            .Returns((Exam?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.Handle(new EnrollStudentCommand(Guid.NewGuid(), _student.Value), default));
    }

    [Fact]
    public async Task Handle_WhenAlreadyEnrolled_ThrowsStudentAlreadyEnrolledException()
    {
        _candidates.ExistsAsync(_exam.Id, _student, Arg.Any<CancellationToken>()).Returns(true);

        await Assert.ThrowsAsync<StudentAlreadyEnrolledException>(
            () => _sut.Handle(new EnrollStudentCommand(_exam.Id.Value, _student.Value), default));
    }

    [Fact]
    public async Task Handle_WhenAlreadyEnrolled_DoesNotPersistDuplicate()
    {
        _candidates.ExistsAsync(_exam.Id, _student, Arg.Any<CancellationToken>()).Returns(true);

        try { await _sut.Handle(new EnrollStudentCommand(_exam.Id.Value, _student.Value), default); }
        catch (StudentAlreadyEnrolledException) { }

        await _candidates.DidNotReceive().AddAsync(Arg.Any<ExamCandidate>(), Arg.Any<CancellationToken>());
    }
}
