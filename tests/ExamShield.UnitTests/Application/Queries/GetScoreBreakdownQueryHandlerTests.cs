using ExamShield.Application.Queries.GetScoreBreakdown;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace ExamShield.UnitTests.Application.Queries;

public sealed class GetScoreBreakdownQueryHandlerTests
{
    private readonly IScoreRepository         _scores      = Substitute.For<IScoreRepository>();
    private readonly ICaptureRepository        _captures    = Substitute.For<ICaptureRepository>();
    private readonly IOcrResultRepository      _ocrResults  = Substitute.For<IOcrResultRepository>();
    private readonly IManualReviewRepository   _reviews     = Substitute.For<IManualReviewRepository>();
    private readonly IAnswerKeyRepository      _answerKeys  = Substitute.For<IAnswerKeyRepository>();
    private readonly GetScoreBreakdownQueryHandler _sut;

    private readonly ExamId     _examId     = ExamId.New();
    private readonly StudentId  _studentId  = StudentId.New();
    private readonly DeviceId   _deviceId   = DeviceId.New();
    private readonly CaptureId  _captureId;
    private readonly Capture    _capture;

    private static readonly IReadOnlyDictionary<int, string> Key =
        new Dictionary<int, string> { [1] = "A", [2] = "B", [3] = "C" };

    public GetScoreBreakdownQueryHandlerTests()
    {
        _sut = new GetScoreBreakdownQueryHandler(_scores, _captures, _ocrResults, _reviews, _answerKeys);

        _capture = Capture.Create(_examId, _studentId, _deviceId,
            new PageNumber(1), Hash.FromBytes(new byte[32]), new Signature(new byte[64]));
        _captureId = _capture.Id;

        _captures.GetByIdAsync(_captureId, Arg.Any<CancellationToken>()).Returns(_capture);
        _answerKeys.GetEntityByExamIdAsync(_examId, Arg.Any<CancellationToken>())
            .Returns(ExamAnswerKey.Create(_examId, Key));
        _reviews.GetByCaptureIdAsync(_captureId, Arg.Any<CancellationToken>())
            .Returns((ManualReview?)null);
    }

    private Score MakeScore(int correct = 2, int total = 3)
    {
        var answers = new List<ExtractedAnswer>
        {
            new(1, "A", new OcrConfidence(1.0)),
            new(2, "B", new OcrConfidence(1.0)),
            new(3, "X", new OcrConfidence(1.0)),  // wrong
        };
        return Score.Create(_captureId, _examId, _studentId, answers.Take(total).ToList(),
            new AnswerKey(Key.ToDictionary(k => k.Key, k => k.Value)));
    }

    private OcrResult MakeOcrResult()
    {
        var answers = new List<ExtractedAnswer>
        {
            new(1, "A", new OcrConfidence(0.95)),
            new(2, "B", new OcrConfidence(0.90)),
            new(3, "X", new OcrConfidence(0.85)),
        };
        return OcrResult.Create(_captureId, answers);
    }

    [Fact]
    public async Task Handle_NoReview_UsesOcrAnswers()
    {
        var score = MakeScore();
        _scores.GetByCaptureIdAsync(_captureId, Arg.Any<CancellationToken>()).Returns(score);
        _ocrResults.GetByCaptureIdAsync(_captureId, Arg.Any<CancellationToken>()).Returns(MakeOcrResult());

        var result = await _sut.Handle(new GetScoreBreakdownQuery(_captureId.Value), default);

        result.Questions.Should().HaveCount(3);
        result.Questions.Should().OnlyContain(q => q.AnswerSource == "OCR");
    }

    [Fact]
    public async Task Handle_CompletedReview_UsesReviewedAnswers()
    {
        var ocrResult = MakeOcrResult();
        var review = ManualReview.CreateFor(ocrResult);
        review.Complete(
            new List<ReviewedAnswer> { new(1, "A"), new(2, "B"), new(3, "C") },
            UserId.New());

        _scores.GetByCaptureIdAsync(_captureId, Arg.Any<CancellationToken>()).Returns(MakeScore());
        _reviews.GetByCaptureIdAsync(_captureId, Arg.Any<CancellationToken>()).Returns(review);

        var result = await _sut.Handle(new GetScoreBreakdownQuery(_captureId.Value), default);

        result.Questions.Should().OnlyContain(q => q.AnswerSource == "ManualReview");
    }

    [Fact]
    public async Task Handle_CorrectAnswers_MarkedIsCorrectTrue()
    {
        _scores.GetByCaptureIdAsync(_captureId, Arg.Any<CancellationToken>()).Returns(MakeScore());
        _ocrResults.GetByCaptureIdAsync(_captureId, Arg.Any<CancellationToken>()).Returns(MakeOcrResult());

        var result = await _sut.Handle(new GetScoreBreakdownQuery(_captureId.Value), default);

        result.Questions.First(q => q.QuestionNumber == 1).IsCorrect.Should().BeTrue();
        result.Questions.First(q => q.QuestionNumber == 2).IsCorrect.Should().BeTrue();
        result.Questions.First(q => q.QuestionNumber == 3).IsCorrect.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ReturnsScoreSummary_FromScore()
    {
        var score = MakeScore(correct: 2, total: 3);
        _scores.GetByCaptureIdAsync(_captureId, Arg.Any<CancellationToken>()).Returns(score);
        _ocrResults.GetByCaptureIdAsync(_captureId, Arg.Any<CancellationToken>()).Returns(MakeOcrResult());

        var result = await _sut.Handle(new GetScoreBreakdownQuery(_captureId.Value), default);

        result.CaptureId.Should().Be(_captureId.Value);
        result.ExamId.Should().Be(_examId.Value);
    }

    [Fact]
    public async Task Handle_ScoreNotFound_ThrowsKeyNotFoundException()
    {
        _scores.GetByCaptureIdAsync(_captureId, Arg.Any<CancellationToken>())
            .Returns((Score?)null);

        var act = async () => await _sut.Handle(new GetScoreBreakdownQuery(_captureId.Value), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_AnswerKeyNotFound_ThrowsKeyNotFoundException()
    {
        _scores.GetByCaptureIdAsync(_captureId, Arg.Any<CancellationToken>()).Returns(MakeScore());
        _answerKeys.GetEntityByExamIdAsync(_examId, Arg.Any<CancellationToken>())
            .Returns((ExamAnswerKey?)null);

        var act = async () => await _sut.Handle(new GetScoreBreakdownQuery(_captureId.Value), default);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
