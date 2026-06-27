using ExamShield.Application.Commands.ScoreCapture;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands;

public sealed class ScoreUsesReviewedAnswersTests
{
    private readonly ICaptureRepository      _captures   = Substitute.For<ICaptureRepository>();
    private readonly IOcrResultRepository    _ocrResults = Substitute.For<IOcrResultRepository>();
    private readonly IAnswerKeyRepository    _answerKeys = Substitute.For<IAnswerKeyRepository>();
    private readonly IScoreRepository        _scores     = Substitute.For<IScoreRepository>();
    private readonly IAuditLogRepository     _auditLog   = Substitute.For<IAuditLogRepository>();
    private readonly ICacheService           _cache      = Substitute.For<ICacheService>();
    private readonly IManualReviewRepository _reviews    = Substitute.For<IManualReviewRepository>();
    private readonly ScoreCaptureCommandHandler _sut;

    private static readonly ExamId ExamId = ExamId.New();

    public ScoreUsesReviewedAnswersTests()
    {
        _sut = new ScoreCaptureCommandHandler(
            _captures, _ocrResults, _answerKeys, _scores, _auditLog, _cache, _reviews);
    }

    private Capture MakeCapture()
    {
        var c = Capture.Create(
            ExamId, StudentId.New(), DeviceId.New(),
            new PageNumber(1), Hash.FromBytes(new byte[32]), new Signature(new byte[64]));
        c.RecordUpload("key.jpg");
        _captures.GetByIdAsync(c.Id, Arg.Any<CancellationToken>()).Returns(c);
        _scores.ExistsByCaptureIdAsync(c.Id, Arg.Any<CancellationToken>()).Returns(false);
        return c;
    }

    private static OcrResult MakeOcrResult(CaptureId captureId) =>
        OcrResult.Create(captureId, [
            new ExtractedAnswer(1, "A", new OcrConfidence(0.45)),  // low confidence
        ]);

    private static ManualReview MakeCompletedReview(OcrResult ocr)
    {
        var review = ManualReview.CreateFor(ocr);
        review.Complete(
            [new ReviewedAnswer(1, "B")],  // reviewer corrected: A → B
            new UserId(Guid.NewGuid()));
        return review;
    }

    [Fact]
    public async Task Handle_WhenCompletedReviewExists_UsesReviewedAnswersForScoring()
    {
        var capture   = MakeCapture();
        var ocrResult = MakeOcrResult(capture.Id);
        var review    = MakeCompletedReview(ocrResult);

        _ocrResults.GetByCaptureIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(ocrResult);
        _reviews.GetByCaptureIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(review);
        _answerKeys.GetByExamIdAsync(ExamId, Arg.Any<CancellationToken>())
                   .Returns(new AnswerKey(new Dictionary<int, string> { [1] = "B" }));

        var result = await _sut.Handle(new ScoreCaptureCommand(capture.Id.Value), default);

        // Reviewer said "B", answer key says "B" → 1 correct
        result.CorrectAnswers.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenCompletedReviewExists_DoesNotUseOriginalOcrAnswers()
    {
        var capture   = MakeCapture();
        var ocrResult = MakeOcrResult(capture.Id);
        var review    = MakeCompletedReview(ocrResult);

        _ocrResults.GetByCaptureIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(ocrResult);
        _reviews.GetByCaptureIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(review);
        _answerKeys.GetByExamIdAsync(ExamId, Arg.Any<CancellationToken>())
                   .Returns(new AnswerKey(new Dictionary<int, string> { [1] = "A" }));

        var result = await _sut.Handle(new ScoreCaptureCommand(capture.Id.Value), default);

        // Original OCR said "A", answer key says "A" → would be 1 correct with original
        // Reviewer corrected to "B" → 0 correct (reviewer's answer is used)
        result.CorrectAnswers.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenNoReviewExists_UsesOcrAnswers()
    {
        var capture   = MakeCapture();
        var ocrResult = OcrResult.Create(capture.Id, [
            new ExtractedAnswer(1, "A", new OcrConfidence(0.95)),
        ]);

        _ocrResults.GetByCaptureIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(ocrResult);
        _reviews.GetByCaptureIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns((ManualReview?)null);
        _answerKeys.GetByExamIdAsync(ExamId, Arg.Any<CancellationToken>())
                   .Returns(new AnswerKey(new Dictionary<int, string> { [1] = "A" }));

        var result = await _sut.Handle(new ScoreCaptureCommand(capture.Id.Value), default);

        result.CorrectAnswers.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenReviewIsPending_UsesOcrAnswers()
    {
        var capture   = MakeCapture();
        var ocrResult = MakeOcrResult(capture.Id);
        var pendingReview = ManualReview.CreateFor(ocrResult); // not yet completed

        _ocrResults.GetByCaptureIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(ocrResult);
        _reviews.GetByCaptureIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(pendingReview);
        _answerKeys.GetByExamIdAsync(ExamId, Arg.Any<CancellationToken>())
                   .Returns(new AnswerKey(new Dictionary<int, string> { [1] = "A" }));

        var result = await _sut.Handle(new ScoreCaptureCommand(capture.Id.Value), default);

        // OCR said "A", answer key says "A" → 1 correct (OCR used, review was pending)
        result.CorrectAnswers.Should().Be(1);
    }
}
