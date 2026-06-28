using ExamShield.Application.Queries.GetAllReviewRequests;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetAllReviewRequests;

public sealed class GetAllReviewRequestsQueryHandlerTests
{
    private readonly IReviewRequestRepository _repo = Substitute.For<IReviewRequestRepository>();
    private readonly GetAllReviewRequestsQueryHandler _sut;

    public GetAllReviewRequestsQueryHandlerTests() =>
        _sut = new GetAllReviewRequestsQueryHandler(_repo);

    private static ReviewRequest MakePending() =>
        ReviewRequest.Submit(new StudentId(Guid.NewGuid()), new CaptureId(Guid.NewGuid()), "OCR error");

    [Fact]
    public async Task Handle_NullStatusFilter_PassesNullToRepo()
    {
        _repo.ListAllAsync(null, Arg.Any<CancellationToken>()).Returns(Array.Empty<ReviewRequest>());

        await _sut.Handle(new GetAllReviewRequestsQuery(null), default);

        await _repo.Received(1).ListAllAsync(null, default);
    }

    [Fact]
    public async Task Handle_ValidStatusFilter_ParsesAndPassesToRepo()
    {
        _repo.ListAllAsync(ReviewRequestStatus.Pending, Arg.Any<CancellationToken>())
             .Returns(Array.Empty<ReviewRequest>());

        await _sut.Handle(new GetAllReviewRequestsQuery("Pending"), default);

        await _repo.Received(1).ListAllAsync(ReviewRequestStatus.Pending, default);
    }

    [Fact]
    public async Task Handle_InvalidStatusFilter_PassesNullToRepo()
    {
        _repo.ListAllAsync(null, Arg.Any<CancellationToken>()).Returns(Array.Empty<ReviewRequest>());

        await _sut.Handle(new GetAllReviewRequestsQuery("bogus"), default);

        await _repo.Received(1).ListAllAsync(null, default);
    }

    [Fact]
    public async Task Handle_WithItems_ReturnsMappedDtos()
    {
        var rr = MakePending();
        _repo.ListAllAsync(null, Arg.Any<CancellationToken>()).Returns(new[] { rr });

        var result = await _sut.Handle(new GetAllReviewRequestsQuery(null), default);

        result.Items.Should().HaveCount(1);
        result.Items[0].ReviewRequestId.Should().Be(rr.Id.Value);
        result.Items[0].Status.Should().Be("Pending");
        result.Items[0].Reason.Should().Be("OCR error");
    }
}
