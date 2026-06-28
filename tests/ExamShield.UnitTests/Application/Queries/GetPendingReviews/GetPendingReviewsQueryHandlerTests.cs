using ExamShield.Application.Queries.GetPendingReviews;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetPendingReviews;

public sealed class GetPendingReviewsQueryHandlerTests
{
    private readonly IManualReviewRepository _reviews = Substitute.For<IManualReviewRepository>();
    private readonly GetPendingReviewsQueryHandler _sut;

    public GetPendingReviewsQueryHandlerTests() =>
        _sut = new GetPendingReviewsQueryHandler(_reviews);

    private static ManualReview MakeReview()
    {
        var ocrResult = OcrResult.Create(
            new CaptureId(Guid.NewGuid()),
            [new ExtractedAnswer(1, "A", new OcrConfidence(0.3))]);
        return ManualReview.CreateFor(ocrResult);
    }

    [Fact]
    public async Task Handle_WithPendingReviews_ReturnsMappedDtos()
    {
        var review = MakeReview();
        _reviews.GetPendingAsync(Arg.Any<CancellationToken>()).Returns(new[] { review });

        var result = await _sut.Handle(new GetPendingReviewsQuery(), default);

        result.Reviews.Should().HaveCount(1);
        result.Reviews[0].ReviewId.Should().Be(review.Id.Value);
        result.Reviews[0].CaptureId.Should().Be(review.CaptureId.Value);
        result.Reviews[0].OcrResultId.Should().Be(review.OcrResultId.Value);
    }

    [Fact]
    public async Task Handle_NoReviews_ReturnsEmptyList()
    {
        _reviews.GetPendingAsync(Arg.Any<CancellationToken>())
                .Returns(Array.Empty<ManualReview>());

        var result = await _sut.Handle(new GetPendingReviewsQuery(), default);

        result.Reviews.Should().BeEmpty();
    }
}
