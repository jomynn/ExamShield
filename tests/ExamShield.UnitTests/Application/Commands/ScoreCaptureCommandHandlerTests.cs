using ExamShield.Application;
using ExamShield.Application.Commands.ScoreCapture;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands;

public sealed class ScoreCaptureCommandHandlerTests
{
    private readonly ICaptureRepository _captures = Substitute.For<ICaptureRepository>();
    private readonly IOcrResultRepository _ocrResults = Substitute.For<IOcrResultRepository>();
    private readonly IAnswerKeyRepository _answerKeys = Substitute.For<IAnswerKeyRepository>();
    private readonly IScoreRepository _scores = Substitute.For<IScoreRepository>();
    private readonly IAuditLogRepository _auditLog = Substitute.For<IAuditLogRepository>();
    private readonly ICacheService           _cache   = Substitute.For<ICacheService>();
    private readonly IManualReviewRepository _reviews = Substitute.For<IManualReviewRepository>();
    private readonly ScoreCaptureCommandHandler _sut;

    private static readonly AnswerKey Key = new(
        new Dictionary<int, string> { [1] = "A", [2] = "B", [3] = "C" });

    public ScoreCaptureCommandHandlerTests() =>
        _sut = new ScoreCaptureCommandHandler(_captures, _ocrResults, _answerKeys, _scores, _auditLog, _cache, _reviews);

    private static Capture UploadedCapture()
    {
        var capture = Capture.Create(ExamId.New(), StudentId.New(), DeviceId.New(),
            new PageNumber(1), Hash.FromHex(new string('a', 64)), new Signature(new byte[64]));
        capture.RecordUpload("storage/key");
        return capture;
    }

    private static OcrResult HighConfidenceOcrResult(CaptureId captureId) =>
        OcrResult.Create(captureId, new[]
        {
            new ExtractedAnswer(1, "A", new OcrConfidence(0.95)),
            new ExtractedAnswer(2, "B", new OcrConfidence(0.90)),
            new ExtractedAnswer(3, "C", new OcrConfidence(0.88))
        });

    [Fact]
    public async Task Handle_WithValidInputs_StoresScore()
    {
        var capture = UploadedCapture();
        var ocr = HighConfidenceOcrResult(capture.Id);
        _captures.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);
        _ocrResults.GetByCaptureIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(ocr);
        _answerKeys.GetByExamIdAsync(capture.ExamId, Arg.Any<CancellationToken>()).Returns(Key);

        await _sut.Handle(new ScoreCaptureCommand(capture.Id.Value), default);

        await _scores.Received(1).AddAsync(Arg.Any<Score>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithAllCorrectAnswers_ReturnsFullScore()
    {
        var capture = UploadedCapture();
        var ocr = HighConfidenceOcrResult(capture.Id);
        _captures.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);
        _ocrResults.GetByCaptureIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(ocr);
        _answerKeys.GetByExamIdAsync(capture.ExamId, Arg.Any<CancellationToken>()).Returns(Key);

        var result = await _sut.Handle(new ScoreCaptureCommand(capture.Id.Value), default);

        result.CorrectAnswers.Should().Be(3);
        result.TotalQuestions.Should().Be(3);
    }

    [Fact]
    public async Task Handle_WhenCaptureNotFound_ThrowsCaptureNotFoundException()
    {
        _captures.GetByIdAsync(Arg.Any<CaptureId>(), Arg.Any<CancellationToken>())
            .Returns((Capture?)null);

        await Assert.ThrowsAsync<CaptureNotFoundException>(() =>
            _sut.Handle(new ScoreCaptureCommand(Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Handle_WhenOcrResultNotFound_ThrowsOcrResultNotFoundException()
    {
        var capture = UploadedCapture();
        _captures.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);
        _ocrResults.GetByCaptureIdAsync(capture.Id, Arg.Any<CancellationToken>())
            .Returns((OcrResult?)null);

        await Assert.ThrowsAsync<OcrResultNotFoundException>(() =>
            _sut.Handle(new ScoreCaptureCommand(capture.Id.Value), default));
    }

    [Fact]
    public async Task Handle_AfterScoring_AppendsScoreGeneratedAuditEntry()
    {
        var capture = UploadedCapture();
        var ocr = HighConfidenceOcrResult(capture.Id);
        _captures.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);
        _ocrResults.GetByCaptureIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(ocr);
        _answerKeys.GetByExamIdAsync(capture.ExamId, Arg.Any<CancellationToken>()).Returns(Key);

        await _sut.Handle(new ScoreCaptureCommand(capture.Id.Value), default);

        await _auditLog.Received(1).AppendAsync(
            Arg.Is<AuditLog>(e => e.Action == AuditAction.ScoreGenerated && e.CaptureId == capture.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AfterScoring_InvalidatesStatisticsCache()
    {
        var capture = UploadedCapture();
        var ocr = HighConfidenceOcrResult(capture.Id);
        _captures.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);
        _ocrResults.GetByCaptureIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(ocr);
        _answerKeys.GetByExamIdAsync(capture.ExamId, Arg.Any<CancellationToken>()).Returns(Key);

        await _sut.Handle(new ScoreCaptureCommand(capture.Id.Value), default);

        await _cache.Received(1).InvalidateAsync(CacheKeys.Statistics, Arg.Any<CancellationToken>());
    }
}
