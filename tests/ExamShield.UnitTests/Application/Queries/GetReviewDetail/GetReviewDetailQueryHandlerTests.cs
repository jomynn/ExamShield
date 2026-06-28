using ExamShield.Application.Queries.GetReviewDetail;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetReviewDetail;

public sealed class GetReviewDetailQueryHandlerTests
{
    private readonly IManualReviewRepository _reviews = Substitute.For<IManualReviewRepository>();
    private readonly IOcrResultRepository _ocrResults = Substitute.For<IOcrResultRepository>();
    private readonly GetReviewDetailQueryHandler _sut;

    public GetReviewDetailQueryHandlerTests() =>
        _sut = new GetReviewDetailQueryHandler(_reviews, _ocrResults);

    private static (ManualReview review, OcrResult ocr) MakePair()
    {
        var ocr = OcrResult.Create(
            new CaptureId(Guid.NewGuid()),
            [new ExtractedAnswer(1, "A", new OcrConfidence(0.9)),
             new ExtractedAnswer(2, "B", new OcrConfidence(0.8))]);
        var review = ManualReview.CreateFor(ocr);
        return (review, ocr);
    }

    [Fact]
    public async Task Handle_WithReviewAndOcr_ReturnsMappedResult()
    {
        var (review, ocr) = MakePair();
        _reviews.GetByIdAsync(review.Id, Arg.Any<CancellationToken>()).Returns(review);
        _ocrResults.GetByIdAsync(review.OcrResultId, Arg.Any<CancellationToken>()).Returns(ocr);

        var result = await _sut.Handle(new GetReviewDetailQuery(review.Id.Value), default);

        result.ReviewId.Should().Be(review.Id.Value);
        result.CaptureId.Should().Be(review.CaptureId.Value);
        result.OcrResultId.Should().Be(review.OcrResultId.Value);
        result.OcrAnswers.Should().HaveCount(2);
        result.OcrAnswers[0].QuestionNumber.Should().Be(1);
        result.OcrAnswers[0].Text.Should().Be("A");
    }

    [Fact]
    public async Task Handle_WhenOcrNull_ReturnsEmptyAnswers()
    {
        var (review, _) = MakePair();
        _reviews.GetByIdAsync(review.Id, Arg.Any<CancellationToken>()).Returns(review);
        _ocrResults.GetByIdAsync(Arg.Any<OcrResultId>(), Arg.Any<CancellationToken>())
                   .Returns((OcrResult?)null);

        var result = await _sut.Handle(new GetReviewDetailQuery(review.Id.Value), default);

        result.OcrAnswers.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReviewNotFound_ThrowsManualReviewNotFoundException()
    {
        _reviews.GetByIdAsync(Arg.Any<ManualReviewId>(), Arg.Any<CancellationToken>())
                .Returns((ManualReview?)null);

        var act = () => _sut.Handle(new GetReviewDetailQuery(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<ManualReviewNotFoundException>();
    }
}
