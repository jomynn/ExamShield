using ExamShield.Application.Commands.EnrollStudent;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands;

public sealed class EnrollStudentCapacityTests
{
    private readonly IExamRepository _exams = Substitute.For<IExamRepository>();
    private readonly IExamCandidateRepository _candidates = Substitute.For<IExamCandidateRepository>();
    private readonly IAuditLogRepository _audit = Substitute.For<IAuditLogRepository>();
    private readonly EnrollStudentCommandHandler _sut;

    public EnrollStudentCapacityTests() =>
        _sut = new EnrollStudentCommandHandler(_exams, _candidates, _audit);

    private static Exam MakeExam(int? maxCandidates = null)
    {
        var exam = Exam.Create("Cap Test", null, 5, maxCandidates: maxCandidates);
        exam.Activate();
        return exam;
    }

    [Fact]
    public async Task Enroll_WhenExamFull_ThrowsExamFullException()
    {
        var exam = MakeExam(maxCandidates: 2);
        _exams.GetByIdAsync(exam.Id, default).Returns(exam);
        _candidates.ExistsAsync(exam.Id, Arg.Any<StudentId>(), default).Returns(false);
        _candidates.CountByExamIdAsync(exam.Id, default).Returns(2);

        await Assert.ThrowsAsync<ExamFullException>(() =>
            _sut.Handle(new EnrollStudentCommand(exam.Id.Value, Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Enroll_WhenCapacityNotReached_Succeeds()
    {
        var exam = MakeExam(maxCandidates: 5);
        _exams.GetByIdAsync(exam.Id, default).Returns(exam);
        _candidates.ExistsAsync(exam.Id, Arg.Any<StudentId>(), default).Returns(false);
        _candidates.CountByExamIdAsync(exam.Id, default).Returns(3);

        await _sut.Handle(new EnrollStudentCommand(exam.Id.Value, Guid.NewGuid()), default);

        await _candidates.Received(1).AddAsync(Arg.Any<ExamCandidate>(), default);
    }

    [Fact]
    public async Task Enroll_WhenNoMaxCandidates_AlwaysSucceeds()
    {
        var exam = MakeExam(maxCandidates: null);
        _exams.GetByIdAsync(exam.Id, default).Returns(exam);
        _candidates.ExistsAsync(exam.Id, Arg.Any<StudentId>(), default).Returns(false);

        await _sut.Handle(new EnrollStudentCommand(exam.Id.Value, Guid.NewGuid()), default);

        await _candidates.DidNotReceive().CountByExamIdAsync(Arg.Any<ExamId>(), default);
        await _candidates.Received(1).AddAsync(Arg.Any<ExamCandidate>(), default);
    }
}
