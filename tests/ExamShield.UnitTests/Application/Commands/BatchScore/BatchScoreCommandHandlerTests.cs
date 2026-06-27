using ExamShield.Application.Commands.BatchScore;
using ExamShield.Application.Commands.ScoreCapture;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.BatchScore;

public sealed class BatchScoreCommandHandlerTests
{
    private readonly IExamRepository     _exams    = Substitute.For<IExamRepository>();
    private readonly ICaptureRepository  _captures = Substitute.For<ICaptureRepository>();
    private readonly IOcrResultRepository _ocr     = Substitute.For<IOcrResultRepository>();
    private readonly IScoreRepository    _scores   = Substitute.For<IScoreRepository>();
    private readonly ISender             _sender   = Substitute.For<ISender>();
    private readonly BatchScoreCommandHandler _sut;

    private readonly Exam _exam;

    public BatchScoreCommandHandlerTests()
    {
        _exam = Exam.Create("Batch Score Test", null, 3);
        _exam.Activate();
        _exams.GetByIdAsync(Arg.Any<ExamId>(), Arg.Any<CancellationToken>()).Returns(_exam);

        _scores.GetByExamIdAsync(_exam.Id, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Score>());

        _sender.Send(Arg.Any<ScoreCaptureCommand>(), Arg.Any<CancellationToken>())
            .Returns(new ScoreCaptureResult(Guid.NewGuid(), 2, 3, 66.7));

        _sut = new BatchScoreCommandHandler(_exams, _captures, _ocr, _scores, _sender);
    }

    private static Capture MakeUploadedCapture(ExamId examId)
    {
        var hash = Hash.FromBytes(new byte[32]);
        var c = Capture.Create(examId, StudentId.New(), DeviceId.New(),
            new PageNumber(1), hash, new Signature(new byte[64]));
        c.RecordUpload("storage/key");
        return c;
    }

    private static OcrResult MakeCompletedOcr(CaptureId captureId)
    {
        var answers = new[] { new ExtractedAnswer(1, "A", new OcrConfidence(0.95)) };
        return OcrResult.Create(captureId, answers);
    }

    [Fact]
    public async Task Handle_WithCompletedOcrCaptures_ScoresEachAndReturnsCount()
    {
        var c1 = MakeUploadedCapture(_exam.Id);
        var c2 = MakeUploadedCapture(_exam.Id);
        _captures.ListByExamIdAsync(_exam.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { c1, c2 });
        _ocr.ListByCaptureIdsAsync(Arg.Any<IReadOnlyList<CaptureId>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { MakeCompletedOcr(c1.Id), MakeCompletedOcr(c2.Id) });

        var result = await _sut.Handle(new BatchScoreCommand(_exam.Id.Value), default);

        Assert.Equal(2, result.Scored);
        Assert.Equal(0, result.Skipped);
        await _sender.Received(2).Send(Arg.Any<ScoreCaptureCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SkipsAlreadyScoredCaptures()
    {
        var c1 = MakeUploadedCapture(_exam.Id);
        _captures.ListByExamIdAsync(_exam.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { c1 });
        _ocr.ListByCaptureIdsAsync(Arg.Any<IReadOnlyList<CaptureId>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { MakeCompletedOcr(c1.Id) });
        var existingScore = Score.Create(c1.Id, _exam.Id, c1.StudentId, Array.Empty<ExtractedAnswer>(), new AnswerKey(new Dictionary<int, string>()));
        _scores.GetByExamIdAsync(_exam.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { existingScore });

        var result = await _sut.Handle(new BatchScoreCommand(_exam.Id.Value), default);

        Assert.Equal(0, result.Scored);
        Assert.Equal(1, result.Skipped);
        await _sender.DidNotReceive().Send(Arg.Any<ScoreCaptureCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SkipsCapturesWithNoOcrResult()
    {
        var c1 = MakeUploadedCapture(_exam.Id);
        _captures.ListByExamIdAsync(_exam.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { c1 });
        _ocr.ListByCaptureIdsAsync(Arg.Any<IReadOnlyList<CaptureId>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<OcrResult>());

        var result = await _sut.Handle(new BatchScoreCommand(_exam.Id.Value), default);

        Assert.Equal(0, result.Scored);
        Assert.Equal(1, result.Skipped);
        await _sender.DidNotReceive().Send(Arg.Any<ScoreCaptureCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenExamNotFound_ThrowsKeyNotFoundException()
    {
        _exams.GetByIdAsync(Arg.Any<ExamId>(), Arg.Any<CancellationToken>())
            .Returns((Exam?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.Handle(new BatchScoreCommand(Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Handle_WithMixedCaptures_OnlyScoresEligible()
    {
        var eligible   = MakeUploadedCapture(_exam.Id);
        var noOcr      = MakeUploadedCapture(_exam.Id);
        var alreadyDone = MakeUploadedCapture(_exam.Id);

        _captures.ListByExamIdAsync(_exam.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { eligible, noOcr, alreadyDone });
        _ocr.ListByCaptureIdsAsync(Arg.Any<IReadOnlyList<CaptureId>>(), Arg.Any<CancellationToken>())
            .Returns(new[] { MakeCompletedOcr(eligible.Id) });
        var existingScore = Score.Create(alreadyDone.Id, _exam.Id, alreadyDone.StudentId,
            Array.Empty<ExtractedAnswer>(), new AnswerKey(new Dictionary<int, string>()));
        _scores.GetByExamIdAsync(_exam.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { existingScore });

        var result = await _sut.Handle(new BatchScoreCommand(_exam.Id.Value), default);

        Assert.Equal(1, result.Scored);
        Assert.Equal(2, result.Skipped);
        await _sender.Received(1).Send(Arg.Any<ScoreCaptureCommand>(), Arg.Any<CancellationToken>());
    }
}
