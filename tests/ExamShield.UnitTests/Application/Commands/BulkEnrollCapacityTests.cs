using ExamShield.Application.Commands.BulkEnrollStudents;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands;

public sealed class BulkEnrollCapacityTests
{
    private readonly IExamRepository _exams = Substitute.For<IExamRepository>();
    private readonly IExamCandidateRepository _candidates = Substitute.For<IExamCandidateRepository>();
    private readonly IAuditLogRepository _audit = Substitute.For<IAuditLogRepository>();
    private readonly BulkEnrollStudentsCommandHandler _sut;

    public BulkEnrollCapacityTests() =>
        _sut = new BulkEnrollStudentsCommandHandler(_exams, _candidates, _audit);

    private static Exam MakeExam(int? maxCandidates)
    {
        var exam = Exam.Create("Bulk Cap Exam", null, 5, maxCandidates: maxCandidates);
        exam.Activate();
        return exam;
    }

    [Fact]
    public async Task BulkEnroll_WhenBatchWouldExceedCap_ThrowsExamFullException()
    {
        var exam = MakeExam(maxCandidates: 2);
        _exams.GetByIdAsync(exam.Id, default).Returns(exam);
        _candidates.ExistsAsync(exam.Id, Arg.Any<StudentId>(), default).Returns(false);
        _candidates.CountByExamIdAsync(exam.Id, default).Returns(2);

        await Assert.ThrowsAsync<ExamFullException>(() =>
            _sut.Handle(new BulkEnrollStudentsCommand(exam.Id.Value,
                [Guid.NewGuid(), Guid.NewGuid()]), default));
    }

    [Fact]
    public async Task BulkEnroll_WhenBatchFitsWithinCap_EnrollsAll()
    {
        var exam = MakeExam(maxCandidates: 5);
        _exams.GetByIdAsync(exam.Id, default).Returns(exam);
        _candidates.ExistsAsync(exam.Id, Arg.Any<StudentId>(), default).Returns(false);
        _candidates.CountByExamIdAsync(exam.Id, default).Returns(3);

        var result = await _sut.Handle(new BulkEnrollStudentsCommand(exam.Id.Value,
            [Guid.NewGuid(), Guid.NewGuid()]), default);

        Assert.Equal(2, result.Enrolled);
    }

    [Fact]
    public async Task BulkEnroll_WithNoCap_EnrollsWithoutCounting()
    {
        var exam = MakeExam(maxCandidates: null);
        _exams.GetByIdAsync(exam.Id, default).Returns(exam);
        _candidates.ExistsAsync(exam.Id, Arg.Any<StudentId>(), default).Returns(false);

        await _sut.Handle(new BulkEnrollStudentsCommand(exam.Id.Value,
            [Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid()]), default);

        await _candidates.DidNotReceive().CountByExamIdAsync(Arg.Any<ExamId>(), default);
        await _candidates.Received(3).AddAsync(Arg.Any<ExamCandidate>(), default);
    }

    [Fact]
    public async Task BulkEnroll_AlreadyEnrolledDontCountTowardsCap()
    {
        var exam = MakeExam(maxCandidates: 3);
        var existingId = Guid.NewGuid();
        _exams.GetByIdAsync(exam.Id, default).Returns(exam);
        // 2 already enrolled in DB, 1 of the batch students is already in there too
        _candidates.CountByExamIdAsync(exam.Id, default).Returns(2);
        _candidates.ExistsAsync(exam.Id, new StudentId(existingId), default).Returns(true);
        _candidates.ExistsAsync(exam.Id, Arg.Is<StudentId>(s => s.Value != existingId), default).Returns(false);

        // 1 already enrolled (skip) + 1 new: total would be 3, which equals cap
        var result = await _sut.Handle(new BulkEnrollStudentsCommand(exam.Id.Value,
            [existingId, Guid.NewGuid()]), default);

        Assert.Equal(1, result.Enrolled);
        Assert.Equal(1, result.AlreadyEnrolled);
    }
}
