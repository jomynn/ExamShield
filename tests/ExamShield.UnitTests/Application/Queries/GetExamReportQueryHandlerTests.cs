using ExamShield.Application.Queries.GetExamReport;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries;

public sealed class GetExamReportQueryHandlerTests
{
    private readonly IExamRepository _exams = Substitute.For<IExamRepository>();
    private readonly ICaptureRepository _captures = Substitute.For<ICaptureRepository>();
    private readonly IOcrResultRepository _ocr = Substitute.For<IOcrResultRepository>();
    private readonly IScoreRepository _scores = Substitute.For<IScoreRepository>();
    private readonly IReviewRequestRepository _reviewRequests = Substitute.For<IReviewRequestRepository>();
    private readonly GetExamReportQueryHandler _sut;

    private readonly Exam _exam;
    private readonly ExamId _examId;

    public GetExamReportQueryHandlerTests()
    {
        _sut = new GetExamReportQueryHandler(_exams, _captures, _ocr, _scores, _reviewRequests);
        _exam = Exam.Create("Maths Final", null, 50);
        _exam.Activate();
        _examId = _exam.Id;
        _exams.GetByIdAsync(_examId, Arg.Any<CancellationToken>()).Returns(_exam);
        _exams.GetByIdAsync(Arg.Is<ExamId>(id => id != _examId), Arg.Any<CancellationToken>())
            .Returns((Exam?)null);
        _captures.ListByExamIdAsync(_examId, Arg.Any<CancellationToken>())
            .Returns(new List<Capture>());
        _ocr.ListByCaptureIdsAsync(Arg.Any<IReadOnlyList<CaptureId>>(), Arg.Any<CancellationToken>())
            .Returns(new List<OcrResult>());
        _scores.GetByExamIdAsync(_examId, Arg.Any<CancellationToken>())
            .Returns(new List<Score>());
        _reviewRequests.ListByCaptureIdsAsync(Arg.Any<IReadOnlyList<CaptureId>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ReviewRequest>());
    }

    [Fact]
    public async Task Handle_ReturnsExamName()
    {
        var result = await _sut.Handle(new GetExamReportQuery(_examId.Value), default);

        result.ExamName.Should().Be("Maths Final");
    }

    [Fact]
    public async Task Handle_WithNoCaptures_ReturnZeroCaptureCount()
    {
        var result = await _sut.Handle(new GetExamReportQuery(_examId.Value), default);

        result.TotalCaptures.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithCaptures_ReturnCorrectCaptureCount()
    {
        var capture = MakeCapture(_examId);
        _captures.ListByExamIdAsync(_examId, Arg.Any<CancellationToken>())
            .Returns(new List<Capture> { capture });

        var result = await _sut.Handle(new GetExamReportQuery(_examId.Value), default);

        result.TotalCaptures.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithOcrResults_ComputesAverageConfidence()
    {
        var capture = MakeCapture(_examId);
        _captures.ListByExamIdAsync(_examId, Arg.Any<CancellationToken>())
            .Returns(new List<Capture> { capture });
        var ocrResult = OcrResult.Create(capture.Id,
            [new ExtractedAnswer(1, "A", new OcrConfidence(0.80))]);
        _ocr.ListByCaptureIdsAsync(Arg.Any<IReadOnlyList<CaptureId>>(), Arg.Any<CancellationToken>())
            .Returns(new List<OcrResult> { ocrResult });

        var result = await _sut.Handle(new GetExamReportQuery(_examId.Value), default);

        result.OcrAverageConfidence.Should().BeApproximately(0.80, 0.01);
    }

    [Fact]
    public async Task Handle_WhenExamNotFound_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.Handle(new GetExamReportQuery(Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Handle_WithScores_ReturnsTotalScoredCount()
    {
        var capture = MakeCapture(_examId);
        _captures.ListByExamIdAsync(_examId, Arg.Any<CancellationToken>())
            .Returns(new List<Capture> { capture });
        var answerKey = new AnswerKey(Enumerable.Range(1, 4).ToDictionary(i => i, i => "A"));
        var answers = Enumerable.Range(1, 4).Select(i => new ExtractedAnswer(i, "A", new OcrConfidence(0.9))).ToList();
        var score = Score.Create(capture.Id, _examId, StudentId.New(), answers, answerKey);
        _scores.GetByExamIdAsync(_examId, Arg.Any<CancellationToken>())
            .Returns(new List<Score> { score });

        var result = await _sut.Handle(new GetExamReportQuery(_examId.Value), default);

        result.TotalScored.Should().Be(1);
        result.AverageScorePercentage.Should().BeApproximately(100.0, 0.1);
    }

    [Fact]
    public async Task Handle_WithReviewRequests_CountsThemCorrectly()
    {
        var capture = MakeCapture(_examId);
        _captures.ListByExamIdAsync(_examId, Arg.Any<CancellationToken>())
            .Returns(new List<Capture> { capture });
        var reviewReq = ReviewRequest.Submit(StudentId.New(), capture.Id, "Dispute reason");
        _reviewRequests.ListByCaptureIdsAsync(Arg.Any<IReadOnlyList<CaptureId>>(), Arg.Any<CancellationToken>())
            .Returns(new List<ReviewRequest> { reviewReq });

        var result = await _sut.Handle(new GetExamReportQuery(_examId.Value), default);

        result.TotalReviewRequests.Should().Be(1);
    }

    private static Capture MakeCapture(ExamId examId) =>
        Capture.Create(examId, StudentId.New(), DeviceId.New(),
            new PageNumber(1), Hash.FromHex(new string('a', 64)),
            new Signature(new byte[64]));
}
