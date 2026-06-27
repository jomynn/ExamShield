using ExamShield.Application.Commands.SetAnswerKey;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.SetAnswerKey;

public sealed class SetAnswerKeyCommandHandlerTests
{
    private readonly IExamRepository _exams = Substitute.For<IExamRepository>();
    private readonly IAnswerKeyRepository _answerKeys = Substitute.For<IAnswerKeyRepository>();
    private readonly IAuditLogRepository _auditLog = Substitute.For<IAuditLogRepository>();
    private readonly SetAnswerKeyCommandHandler _sut;

    private readonly Exam _exam;

    public SetAnswerKeyCommandHandlerTests()
    {
        _exam = Exam.Create("Test Exam", null, 3);
        _exams.GetByIdAsync(Arg.Any<ExamId>(), Arg.Any<CancellationToken>())
            .Returns(_exam);
        _answerKeys.GetByExamIdAsync(Arg.Any<ExamId>(), Arg.Any<CancellationToken>())
            .Returns((ExamAnswerKey?)null);

        _sut = new SetAnswerKeyCommandHandler(_exams, _answerKeys, _auditLog);
    }

    [Fact]
    public async Task Handle_WhenExamExistsAndNoKeySet_PersistsAnswerKey()
    {
        var answers = new Dictionary<int, string> { [1] = "A", [2] = "B", [3] = "C" };
        var command = new SetAnswerKeyCommand(_exam.Id.Value, answers);

        await _sut.Handle(command, default);

        await _answerKeys.Received(1).SaveAsync(
            Arg.Is<ExamAnswerKey>(k => k.ExamId == _exam.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenKeyAlreadySet_ThrowsInvalidOperationException()
    {
        var existing = ExamAnswerKey.Create(_exam.Id, new Dictionary<int, string> { [1] = "A" });
        _answerKeys.GetByExamIdAsync(Arg.Any<ExamId>(), Arg.Any<CancellationToken>())
            .Returns(existing);

        var command = new SetAnswerKeyCommand(_exam.Id.Value, new Dictionary<int, string> { [1] = "B" });

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.Handle(command, default));
    }

    [Fact]
    public async Task Handle_WhenExamNotFound_ThrowsKeyNotFoundException()
    {
        _exams.GetByIdAsync(Arg.Any<ExamId>(), Arg.Any<CancellationToken>())
            .Returns((Exam?)null);

        var command = new SetAnswerKeyCommand(Guid.NewGuid(), new Dictionary<int, string> { [1] = "A" });

        await Assert.ThrowsAsync<KeyNotFoundException>(() => _sut.Handle(command, default));
    }

    [Fact]
    public async Task Handle_WhenAnswersEmpty_ThrowsArgumentException()
    {
        var command = new SetAnswerKeyCommand(_exam.Id.Value, new Dictionary<int, string>());

        await Assert.ThrowsAsync<ArgumentException>(() => _sut.Handle(command, default));
    }

    [Fact]
    public async Task Handle_WhenKeySet_AppendsAuditLog()
    {
        var answers = new Dictionary<int, string> { [1] = "A" };
        var command = new SetAnswerKeyCommand(_exam.Id.Value, answers);

        await _sut.Handle(command, default);

        await _auditLog.Received(1).AppendAsync(
            Arg.Any<AuditLog>(), Arg.Any<CancellationToken>());
    }
}
