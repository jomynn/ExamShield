using ExamShield.Application.Queries.GetReviewRequests;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetReviewRequests;

public sealed class GetReviewRequestsQueryHandlerTests
{
    private readonly IReviewRequestRepository _repo = Substitute.For<IReviewRequestRepository>();
    private readonly GetReviewRequestsQueryHandler _sut;

    public GetReviewRequestsQueryHandlerTests() =>
        _sut = new GetReviewRequestsQueryHandler(_repo);

    [Fact]
    public async Task Handle_WithRequests_ReturnsMappedDtos()
    {
        var studentId = new StudentId(Guid.NewGuid());
        var rr = ReviewRequest.Submit(studentId, new CaptureId(Guid.NewGuid()), "Bubble error");
        _repo.ListByStudentAsync(studentId, Arg.Any<CancellationToken>()).Returns(new[] { rr });

        var result = await _sut.Handle(new GetReviewRequestsQuery(studentId.Value), default);

        result.Items.Should().HaveCount(1);
        result.Items[0].ReviewRequestId.Should().Be(rr.Id.Value);
        result.Items[0].StudentId.Should().Be(studentId.Value);
        result.Items[0].Reason.Should().Be("Bubble error");
        result.Items[0].Status.Should().Be("Pending");
    }

    [Fact]
    public async Task Handle_NoRequests_ReturnsEmptyList()
    {
        _repo.ListByStudentAsync(Arg.Any<StudentId>(), Arg.Any<CancellationToken>())
             .Returns(Array.Empty<ReviewRequest>());

        var result = await _sut.Handle(new GetReviewRequestsQuery(Guid.NewGuid()), default);

        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PassesCorrectStudentId()
    {
        var studentId = new StudentId(Guid.NewGuid());
        _repo.ListByStudentAsync(studentId, Arg.Any<CancellationToken>())
             .Returns(Array.Empty<ReviewRequest>());

        await _sut.Handle(new GetReviewRequestsQuery(studentId.Value), default);

        await _repo.Received(1).ListByStudentAsync(studentId, default);
    }
}
