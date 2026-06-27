using ExamShield.Application.Commands.ActivateExam;
using ExamShield.Application.Commands.CloseExam;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace ExamShield.UnitTests.Application.Commands;

public sealed class ExamStateCommandHandlerTests
{
    private readonly IExamRepository _exams = Substitute.For<IExamRepository>();

    private static Exam DraftExam()
    {
        var exam = Exam.Create("Math Final", null, 50);
        return exam;
    }

    private static Exam ActiveExam()
    {
        var exam = Exam.Create("Math Final", null, 50);
        exam.Activate();
        return exam;
    }

    // ── Activate ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task ActivateExam_WhenDraft_UpdatesStatusToActive()
    {
        var exam = DraftExam();
        _exams.GetByIdAsync(exam.Id, Arg.Any<CancellationToken>()).Returns(exam);
        var sut = new ActivateExamCommandHandler(_exams);

        await sut.Handle(new ActivateExamCommand(exam.Id.Value), CancellationToken.None);

        await _exams.Received(1).UpdateAsync(
            Arg.Is<Exam>(e => e.Status == ExamStatus.Active),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ActivateExam_WhenExamNotFound_ThrowsKeyNotFoundException()
    {
        _exams.GetByIdAsync(Arg.Any<ExamId>(), Arg.Any<CancellationToken>()).Returns((Exam?)null);
        var sut = new ActivateExamCommandHandler(_exams);

        var act = () => sut.Handle(new ActivateExamCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task ActivateExam_WhenAlreadyActive_ThrowsInvalidOperationException()
    {
        var exam = ActiveExam();
        _exams.GetByIdAsync(exam.Id, Arg.Any<CancellationToken>()).Returns(exam);
        var sut = new ActivateExamCommandHandler(_exams);

        var act = () => sut.Handle(new ActivateExamCommand(exam.Id.Value), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ── Close ─────────────────────────────────────────────────────────────────

    [Fact]
    public async Task CloseExam_WhenActive_UpdatesStatusToClosed()
    {
        var exam = ActiveExam();
        _exams.GetByIdAsync(exam.Id, Arg.Any<CancellationToken>()).Returns(exam);
        var sut = new CloseExamCommandHandler(_exams);

        await sut.Handle(new CloseExamCommand(exam.Id.Value), CancellationToken.None);

        await _exams.Received(1).UpdateAsync(
            Arg.Is<Exam>(e => e.Status == ExamStatus.Closed),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CloseExam_WhenExamNotFound_ThrowsKeyNotFoundException()
    {
        _exams.GetByIdAsync(Arg.Any<ExamId>(), Arg.Any<CancellationToken>()).Returns((Exam?)null);
        var sut = new CloseExamCommandHandler(_exams);

        var act = () => sut.Handle(new CloseExamCommand(Guid.NewGuid()), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task CloseExam_WhenAlreadyClosed_ThrowsInvalidOperationException()
    {
        var exam = ActiveExam();
        exam.Close();
        _exams.GetByIdAsync(exam.Id, Arg.Any<CancellationToken>()).Returns(exam);
        var sut = new CloseExamCommandHandler(_exams);

        var act = () => sut.Handle(new CloseExamCommand(exam.Id.Value), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
