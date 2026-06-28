using ExamShield.Application.Queries.GetOcrResult;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetOcrResult;

public sealed class GetOcrResultQueryHandlerTests
{
    private readonly IOcrResultRepository _ocrResults = Substitute.For<IOcrResultRepository>();
    private readonly GetOcrResultQueryHandler _sut;

    public GetOcrResultQueryHandlerTests() =>
        _sut = new GetOcrResultQueryHandler(_ocrResults);

    private static OcrResult MakeResult(double confidence = 0.95)
    {
        var captureId = new CaptureId(Guid.NewGuid());
        return OcrResult.Create(captureId, [new ExtractedAnswer(1, "A", new OcrConfidence(confidence))]);
    }

    [Fact]
    public async Task Handle_ExistingResult_ReturnsMappedDto()
    {
        var ocr = MakeResult(0.92);
        _ocrResults.GetByCaptureIdAsync(ocr.CaptureId, Arg.Any<CancellationToken>()).Returns(ocr);

        var result = await _sut.Handle(new GetOcrResultQuery(ocr.CaptureId.Value), default);

        result.OcrResultId.Should().Be(ocr.Id.Value);
        result.CaptureId.Should().Be(ocr.CaptureId.Value);
        result.OverallConfidence.Should().BeApproximately(0.92, 0.01);
        result.Answers.Should().HaveCount(1);
        result.Answers[0].QuestionNumber.Should().Be(1);
        result.Answers[0].Text.Should().Be("A");
    }

    [Fact]
    public async Task Handle_LowConfidence_SetsRequiresManualReview()
    {
        var ocr = MakeResult(0.3);
        _ocrResults.GetByCaptureIdAsync(ocr.CaptureId, Arg.Any<CancellationToken>()).Returns(ocr);

        var result = await _sut.Handle(new GetOcrResultQuery(ocr.CaptureId.Value), default);

        result.RequiresManualReview.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MissingResult_ThrowsOcrResultNotFoundException()
    {
        _ocrResults.GetByCaptureIdAsync(Arg.Any<CaptureId>(), Arg.Any<CancellationToken>())
                   .Returns((OcrResult?)null);

        var act = () => _sut.Handle(new GetOcrResultQuery(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<OcrResultNotFoundException>();
    }
}
