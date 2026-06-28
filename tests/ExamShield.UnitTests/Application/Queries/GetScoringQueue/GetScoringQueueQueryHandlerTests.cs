using ExamShield.Application.Queries.GetScoringQueue;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetScoringQueue;

public sealed class GetScoringQueueQueryHandlerTests
{
    private readonly IOcrResultRepository _ocrResults = Substitute.For<IOcrResultRepository>();
    private readonly IScoreRepository _scores = Substitute.For<IScoreRepository>();
    private readonly ICaptureRepository _captures = Substitute.For<ICaptureRepository>();
    private readonly GetScoringQueueQueryHandler _sut;

    public GetScoringQueueQueryHandlerTests() =>
        _sut = new GetScoringQueueQueryHandler(_ocrResults, _scores, _captures);

    private static OcrResult MakeCompletedOcr(CaptureId? captureId = null)
    {
        var cid = captureId ?? new CaptureId(Guid.NewGuid());
        return OcrResult.Create(cid, [new ExtractedAnswer(1, "A", new OcrConfidence(0.95))]);
    }

    private static Capture MakeCapture(CaptureId captureId) =>
        Capture.Create(
            new ExamId(Guid.NewGuid()), new StudentId(Guid.NewGuid()),
            new DeviceId(Guid.NewGuid()), new PageNumber(1),
            Hash.FromBytes(new byte[32]), new Signature(new byte[64]));

    [Fact]
    public async Task Handle_OcrCompletedNotYetScored_ReturnsItem()
    {
        var ocr = MakeCompletedOcr();
        _ocrResults.ListCompletedAsync(Arg.Any<CancellationToken>()).Returns(new[] { ocr });
        _scores.ExistsByCaptureIdAsync(ocr.CaptureId, Arg.Any<CancellationToken>()).Returns(false);
        _captures.GetByIdAsync(ocr.CaptureId, Arg.Any<CancellationToken>()).Returns((Capture?)null);

        var result = await _sut.Handle(new GetScoringQueueQuery(), default);

        result.Items.Should().HaveCount(1);
        result.Items[0].OcrResultId.Should().Be(ocr.Id.Value);
        result.Items[0].CaptureId.Should().Be(ocr.CaptureId.Value);
    }

    [Fact]
    public async Task Handle_AlreadyScored_ExcludesFromQueue()
    {
        var ocr = MakeCompletedOcr();
        _ocrResults.ListCompletedAsync(Arg.Any<CancellationToken>()).Returns(new[] { ocr });
        _scores.ExistsByCaptureIdAsync(ocr.CaptureId, Arg.Any<CancellationToken>()).Returns(true);

        var result = await _sut.Handle(new GetScoringQueueQuery(), default);

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_NoCompletedOcr_ReturnsEmptyList()
    {
        _ocrResults.ListCompletedAsync(Arg.Any<CancellationToken>()).Returns(Array.Empty<OcrResult>());

        var result = await _sut.Handle(new GetScoringQueueQuery(), default);

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_CaptureExists_PopulatesExamId()
    {
        var captureId = new CaptureId(Guid.NewGuid());
        var ocr = MakeCompletedOcr(captureId);
        var capture = MakeCapture(captureId);

        _ocrResults.ListCompletedAsync(Arg.Any<CancellationToken>()).Returns(new[] { ocr });
        _scores.ExistsByCaptureIdAsync(ocr.CaptureId, Arg.Any<CancellationToken>()).Returns(false);
        _captures.GetByIdAsync(captureId, Arg.Any<CancellationToken>()).Returns(capture);

        var result = await _sut.Handle(new GetScoringQueueQuery(), default);

        result.Items[0].ExamId.Should().Be(capture.ExamId.Value);
    }
}
