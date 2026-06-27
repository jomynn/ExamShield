using ExamShield.Application.Commands.ScoreCapture;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands;

public sealed class PreventDuplicateScoringTests
{
    private readonly ICaptureRepository    _captures   = Substitute.For<ICaptureRepository>();
    private readonly IOcrResultRepository  _ocrResults = Substitute.For<IOcrResultRepository>();
    private readonly IAnswerKeyRepository  _answerKeys = Substitute.For<IAnswerKeyRepository>();
    private readonly IScoreRepository      _scores     = Substitute.For<IScoreRepository>();
    private readonly IAuditLogRepository   _auditLog   = Substitute.For<IAuditLogRepository>();
    private readonly ICacheService           _cache   = Substitute.For<ICacheService>();
    private readonly IManualReviewRepository _reviews = Substitute.For<IManualReviewRepository>();
    private readonly ScoreCaptureCommandHandler _sut;

    public PreventDuplicateScoringTests()
    {
        _sut = new ScoreCaptureCommandHandler(
            _captures, _ocrResults, _answerKeys, _scores, _auditLog, _cache, _reviews);
    }

    private static Capture MakeCapture()
    {
        var c = Capture.Create(
            ExamId.New(), StudentId.New(), DeviceId.New(),
            new PageNumber(1),
            Hash.FromBytes(new byte[32]),
            new Signature(new byte[64]));
        c.RecordUpload("key.jpg");
        return c;
    }

    private static OcrResult MakeOcrResult(CaptureId captureId) =>
        OcrResult.Create(captureId, [new ExtractedAnswer(1, "A", new OcrConfidence(0.95))]);

    [Fact]
    public async Task Handle_WhenScoreAlreadyExists_ThrowsDuplicateScoreException()
    {
        var capture = MakeCapture();
        _captures.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);
        _ocrResults.GetByCaptureIdAsync(capture.Id, Arg.Any<CancellationToken>())
                   .Returns(MakeOcrResult(capture.Id));
        _scores.ExistsByCaptureIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(true);

        var act = () => _sut.Handle(new ScoreCaptureCommand(capture.Id.Value), default);

        await act.Should().ThrowAsync<DuplicateScoreException>();
    }

    [Fact]
    public async Task Handle_WhenScoreAlreadyExists_DoesNotAddScore()
    {
        var capture = MakeCapture();
        _captures.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);
        _ocrResults.GetByCaptureIdAsync(capture.Id, Arg.Any<CancellationToken>())
                   .Returns(MakeOcrResult(capture.Id));
        _scores.ExistsByCaptureIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(true);

        try { await _sut.Handle(new ScoreCaptureCommand(capture.Id.Value), default); } catch { }

        await _scores.DidNotReceive().AddAsync(Arg.Any<Score>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoExistingScore_ScoresAndAdds()
    {
        var capture = MakeCapture();
        _captures.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);
        _ocrResults.GetByCaptureIdAsync(capture.Id, Arg.Any<CancellationToken>())
                   .Returns(MakeOcrResult(capture.Id));
        _scores.ExistsByCaptureIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(false);
        _answerKeys.GetByExamIdAsync(capture.ExamId, Arg.Any<CancellationToken>())
                   .Returns((AnswerKey?)null);

        await _sut.Handle(new ScoreCaptureCommand(capture.Id.Value), default);

        await _scores.Received(1).AddAsync(Arg.Any<Score>(), Arg.Any<CancellationToken>());
    }
}
