using ExamShield.Application.Commands.EnrollStudent;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.EnrollStudent;

public sealed class EnrollStudentClosedExamTests
{
    private readonly IExamRepository          _exams      = Substitute.For<IExamRepository>();
    private readonly IExamCandidateRepository _candidates = Substitute.For<IExamCandidateRepository>();
    private readonly IAuditLogRepository      _audit      = Substitute.For<IAuditLogRepository>();
    private readonly EnrollStudentCommandHandler _sut;

    public EnrollStudentClosedExamTests() =>
        _sut = new EnrollStudentCommandHandler(_exams, _candidates, _audit);

    [Fact]
    public async Task Handle_ClosedExam_ThrowsInvalidOperationException()
    {
        var exam = Exam.Create("Final Exam", null, 10);
        exam.Activate();
        exam.Close();
        _exams.GetByIdAsync(Arg.Any<ExamId>(), default).Returns(exam);

        var act = () => _sut.Handle(
            new EnrollStudentCommand(exam.Id.Value, Guid.NewGuid()), default);

        await act.Should().ThrowAsync<InvalidOperationException>()
                 .WithMessage("*closed*");
    }

    [Fact]
    public async Task Handle_ClosedExam_NeverPersistsCandidate()
    {
        var exam = Exam.Create("Final Exam", null, 10);
        exam.Activate();
        exam.Close();
        _exams.GetByIdAsync(Arg.Any<ExamId>(), default).Returns(exam);

        try { await _sut.Handle(new EnrollStudentCommand(exam.Id.Value, Guid.NewGuid()), default); }
        catch { /* expected */ }

        await _candidates.DidNotReceive().AddAsync(Arg.Any<ExamCandidate>(), default);
    }

    [Fact]
    public async Task Handle_ActiveExam_EnrollsSuccessfully()
    {
        var exam = Exam.Create("Final Exam", null, 10);
        exam.Activate();
        _exams.GetByIdAsync(Arg.Any<ExamId>(), default).Returns(exam);
        _candidates.ExistsAsync(Arg.Any<ExamId>(), Arg.Any<StudentId>(), default).Returns(false);

        var act = () => _sut.Handle(
            new EnrollStudentCommand(exam.Id.Value, Guid.NewGuid()), default);

        await act.Should().NotThrowAsync();
        await _candidates.Received(1).AddAsync(Arg.Any<ExamCandidate>(), default);
    }
}
