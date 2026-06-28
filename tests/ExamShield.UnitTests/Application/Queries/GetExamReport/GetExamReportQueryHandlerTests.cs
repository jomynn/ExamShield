using ExamShield.Application.Queries.GetExamReport;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetExamReport;

public sealed class GetExamReportQueryHandlerTests
{
    private readonly IExamRepository _exams = Substitute.For<IExamRepository>();
    private readonly ICaptureRepository _captures = Substitute.For<ICaptureRepository>();
    private readonly IOcrResultRepository _ocrResults = Substitute.For<IOcrResultRepository>();
    private readonly IScoreRepository _scores = Substitute.For<IScoreRepository>();
    private readonly IReviewRequestRepository _reviewRequests = Substitute.For<IReviewRequestRepository>();
    private readonly GetExamReportQueryHandler _sut;

    public GetExamReportQueryHandlerTests() =>
        _sut = new(_exams, _captures, _ocrResults, _scores, _reviewRequests);

    private Exam SetupExam()
    {
        var exam = Exam.Create("Final Exam", null, 50);
        _exams.GetByIdAsync(exam.Id, default).Returns(exam);
        return exam;
    }

    private void SetupEmptyCaptures(ExamId examId)
    {
        IReadOnlyList<Capture> empty = [];
        _captures.ListByExamIdAsync(examId, default).Returns(empty);
        IReadOnlyList<Score> emptyScores = [];
        _scores.GetByExamIdAsync(examId, default).Returns(emptyScores);
    }

    [Fact]
    public async Task Handle_ExamNotFound_ThrowsKeyNotFoundException()
    {
        _exams.GetByIdAsync(Arg.Any<ExamId>(), default).Returns((Exam?)null);

        await FluentActions.Invoking(() => _sut.Handle(new(Guid.NewGuid()), default))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task Handle_NoCaptures_ReturnsZeroCountsForAllMetrics()
    {
        var exam = SetupExam();
        SetupEmptyCaptures(exam.Id);

        var result = await _sut.Handle(new(exam.Id.Value), default);

        result.TotalCaptures.Should().Be(0);
        result.UploadedCaptures.Should().Be(0);
        result.VerifiedCaptures.Should().Be(0);
        result.TamperedCaptures.Should().Be(0);
        result.TotalOcrProcessed.Should().Be(0);
        result.OcrAverageConfidence.Should().Be(0.0);
        result.TotalScored.Should().Be(0);
        result.AverageScorePercentage.Should().Be(0.0);
        result.TotalReviewRequests.Should().Be(0);
    }

    [Fact]
    public async Task Handle_SetsExamNameAndStatus()
    {
        var exam = SetupExam();
        SetupEmptyCaptures(exam.Id);

        var result = await _sut.Handle(new(exam.Id.Value), default);

        result.ExamName.Should().Be("Final Exam");
        result.ExamStatus.Should().Be("Draft");
        result.TotalQuestions.Should().Be(50);
    }

    [Fact]
    public async Task Handle_CountsUploadedAndVerifiedCaptures()
    {
        var exam = SetupExam();
        var hash = Hash.FromBytes(new byte[32]);
        var sig = new Signature(new byte[64]);

        var capCreated = Capture.Create(exam.Id, new StudentId(Guid.NewGuid()), DeviceId.New(), new PageNumber(1), hash, sig);
        var capUploaded = Capture.Create(exam.Id, new StudentId(Guid.NewGuid()), DeviceId.New(), new PageNumber(1), hash, sig);
        capUploaded.RecordUpload("key1");
        var capVerified = Capture.Create(exam.Id, new StudentId(Guid.NewGuid()), DeviceId.New(), new PageNumber(1), hash, sig);
        capVerified.RecordUpload("key2");
        capVerified.VerifyIntegrity(hash);

        IReadOnlyList<Capture> caps = [capCreated, capUploaded, capVerified];
        _captures.ListByExamIdAsync(exam.Id, default).Returns(caps);
        IReadOnlyList<OcrResult> emptyOcr = [];
        _ocrResults.ListByCaptureIdsAsync(Arg.Any<IReadOnlyList<CaptureId>>(), default).Returns(emptyOcr);
        IReadOnlyList<Score> emptyScores = [];
        _scores.GetByExamIdAsync(exam.Id, default).Returns(emptyScores);
        IReadOnlyList<ReviewRequest> emptyReviews = [];
        _reviewRequests.ListByCaptureIdsAsync(Arg.Any<IReadOnlyList<CaptureId>>(), default).Returns(emptyReviews);

        var result = await _sut.Handle(new(exam.Id.Value), default);

        result.TotalCaptures.Should().Be(3);
        result.UploadedCaptures.Should().Be(2); // uploaded + verified (status >= Uploaded)
        result.VerifiedCaptures.Should().Be(1);
    }

    [Fact]
    public async Task Handle_AveragesOcrConfidence()
    {
        var exam = SetupExam();
        var hash = Hash.FromBytes(new byte[32]);
        var sig = new Signature(new byte[64]);
        var cap = Capture.Create(exam.Id, new StudentId(Guid.NewGuid()), DeviceId.New(), new PageNumber(1), hash, sig);

        IReadOnlyList<Capture> caps = [cap];
        _captures.ListByExamIdAsync(exam.Id, default).Returns(caps);

        var ocr1 = OcrResult.Create(cap.Id, [new ExtractedAnswer(1, "A", new OcrConfidence(0.9))]);
        var ocr2 = OcrResult.Create(cap.Id, [new ExtractedAnswer(1, "A", new OcrConfidence(0.7))]);
        IReadOnlyList<OcrResult> ocrList = [ocr1, ocr2];
        _ocrResults.ListByCaptureIdsAsync(Arg.Any<IReadOnlyList<CaptureId>>(), default).Returns(ocrList);

        IReadOnlyList<Score> emptyScores = [];
        _scores.GetByExamIdAsync(exam.Id, default).Returns(emptyScores);
        IReadOnlyList<ReviewRequest> emptyReviews = [];
        _reviewRequests.ListByCaptureIdsAsync(Arg.Any<IReadOnlyList<CaptureId>>(), default).Returns(emptyReviews);

        var result = await _sut.Handle(new(exam.Id.Value), default);

        result.TotalOcrProcessed.Should().Be(2);
        result.OcrAverageConfidence.Should().BeApproximately(0.8, 0.001);
    }

    [Fact]
    public async Task Handle_CountsLowConfidenceOcrResults()
    {
        var exam = SetupExam();
        var hash = Hash.FromBytes(new byte[32]);
        var sig = new Signature(new byte[64]);
        var cap = Capture.Create(exam.Id, new StudentId(Guid.NewGuid()), DeviceId.New(), new PageNumber(1), hash, sig);

        IReadOnlyList<Capture> caps = [cap];
        _captures.ListByExamIdAsync(exam.Id, default).Returns(caps);

        var lowConf = OcrResult.Create(cap.Id, [new ExtractedAnswer(1, "A", new OcrConfidence(0.3))]);
        var highConf = OcrResult.Create(cap.Id, [new ExtractedAnswer(1, "A", new OcrConfidence(0.95))]);
        IReadOnlyList<OcrResult> ocrList = [lowConf, highConf];
        _ocrResults.ListByCaptureIdsAsync(Arg.Any<IReadOnlyList<CaptureId>>(), default).Returns(ocrList);

        IReadOnlyList<Score> emptyScores = [];
        _scores.GetByExamIdAsync(exam.Id, default).Returns(emptyScores);
        IReadOnlyList<ReviewRequest> emptyReviews = [];
        _reviewRequests.ListByCaptureIdsAsync(Arg.Any<IReadOnlyList<CaptureId>>(), default).Returns(emptyReviews);

        var result = await _sut.Handle(new(exam.Id.Value), default);

        result.LowConfidenceCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_NoCaptures_DoesNotCallOcrOrReviewRepos()
    {
        var exam = SetupExam();
        SetupEmptyCaptures(exam.Id);

        await _sut.Handle(new(exam.Id.Value), default);

        await _ocrResults.DidNotReceive().ListByCaptureIdsAsync(Arg.Any<IReadOnlyList<CaptureId>>(), default);
        await _reviewRequests.DidNotReceive().ListByCaptureIdsAsync(Arg.Any<IReadOnlyList<CaptureId>>(), default);
    }
}
