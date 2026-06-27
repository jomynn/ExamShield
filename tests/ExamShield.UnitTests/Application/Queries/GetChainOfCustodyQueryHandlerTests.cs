using ExamShield.Application.Queries.GetChainOfCustody;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries;

public sealed class GetChainOfCustodyQueryHandlerTests
{
    private readonly ICaptureRepository      _captures    = Substitute.For<ICaptureRepository>();
    private readonly IOcrResultRepository    _ocrResults  = Substitute.For<IOcrResultRepository>();
    private readonly IScoreRepository        _scores      = Substitute.For<IScoreRepository>();
    private readonly IReviewRequestRepository _reviews    = Substitute.For<IReviewRequestRepository>();
    private readonly IAuditLogRepository     _audit       = Substitute.For<IAuditLogRepository>();
    private readonly GetChainOfCustodyQueryHandler _sut;

    public GetChainOfCustodyQueryHandlerTests()
    {
        _ocrResults.GetByCaptureIdAsync(Arg.Any<CaptureId>(), Arg.Any<CancellationToken>())
            .Returns((OcrResult?)null);
        _scores.GetByCaptureIdAsync(Arg.Any<CaptureId>(), Arg.Any<CancellationToken>())
            .Returns((Score?)null);
        _reviews.ListByCaptureIdsAsync(Arg.Any<IReadOnlyList<CaptureId>>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<ReviewRequest>());
        _audit.GetChainAsync(Arg.Any<CaptureId>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<AuditLog>());

        _sut = new GetChainOfCustodyQueryHandler(_captures, _ocrResults, _scores, _reviews, _audit);
    }

    private static Capture MakeCapture()
    {
        var hash = Hash.FromBytes(new byte[32]);
        return Capture.Create(ExamId.New(), StudentId.New(), DeviceId.New(),
            new PageNumber(1), hash, new Signature(new byte[64]));
    }

    [Fact]
    public async Task Handle_CaptureNotFound_ThrowsCaptureNotFoundException()
    {
        _captures.GetByIdAsync(Arg.Any<CaptureId>(), Arg.Any<CancellationToken>())
            .Returns((Capture?)null);

        await Assert.ThrowsAsync<CaptureNotFoundException>(
            () => _sut.Handle(new GetChainOfCustodyQuery(Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Handle_ValidCapture_ReturnsCaptureInfo()
    {
        var capture = MakeCapture();
        _captures.GetByIdAsync(Arg.Any<CaptureId>(), Arg.Any<CancellationToken>()).Returns(capture);

        var result = await _sut.Handle(new GetChainOfCustodyQuery(capture.Id.Value), default);

        Assert.Equal(capture.Id.Value,       result.CaptureId);
        Assert.Equal(capture.ExamId.Value,   result.ExamId);
        Assert.Equal(capture.StudentId.Value, result.StudentId);
        Assert.Equal(capture.DeviceId.Value,  result.DeviceId);
        Assert.Equal(capture.Status.ToString(), result.Status);
    }

    [Fact]
    public async Task Handle_WithOcrResult_PopulatesOcrSection()
    {
        var capture = MakeCapture();
        _captures.GetByIdAsync(Arg.Any<CaptureId>(), Arg.Any<CancellationToken>()).Returns(capture);

        var ocr = OcrResult.Create(capture.Id,
            new[] { new ExtractedAnswer(1, "A", new OcrConfidence(0.95)) });
        _ocrResults.GetByCaptureIdAsync(Arg.Any<CaptureId>(), Arg.Any<CancellationToken>()).Returns(ocr);

        var result = await _sut.Handle(new GetChainOfCustodyQuery(capture.Id.Value), default);

        Assert.NotNull(result.OcrResult);
        Assert.Equal(0.95, result.OcrResult!.OverallConfidence);
    }

    [Fact]
    public async Task Handle_WithScore_PopulatesScoreSection()
    {
        var capture = MakeCapture();
        _captures.GetByIdAsync(Arg.Any<CaptureId>(), Arg.Any<CancellationToken>()).Returns(capture);

        var key   = new AnswerKey(new Dictionary<int, string> { [1] = "A" });
        var score = Score.Create(capture.Id, capture.ExamId, capture.StudentId,
            Array.Empty<ExtractedAnswer>(), key);
        _scores.GetByCaptureIdAsync(Arg.Any<CaptureId>(), Arg.Any<CancellationToken>()).Returns(score);

        var result = await _sut.Handle(new GetChainOfCustodyQuery(capture.Id.Value), default);

        Assert.NotNull(result.Score);
        Assert.Equal(score.Percentage, result.Score!.Percentage);
    }

    [Fact]
    public async Task Handle_AuditEntriesIncludedInTimeline()
    {
        var capture = MakeCapture();
        _captures.GetByIdAsync(Arg.Any<CaptureId>(), Arg.Any<CancellationToken>()).Returns(capture);

        var entry = AuditLog.Record(AuditAction.CaptureRegistered, captureId: capture.Id);
        _audit.GetChainAsync(Arg.Any<CaptureId>(), Arg.Any<CancellationToken>())
            .Returns(new[] { entry });

        var result = await _sut.Handle(new GetChainOfCustodyQuery(capture.Id.Value), default);

        Assert.Single(result.AuditTrail);
        Assert.Equal("CaptureRegistered", result.AuditTrail[0].Action);
    }
}
