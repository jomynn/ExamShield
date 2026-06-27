using ExamShield.Application.Commands.SubmitReviewRequest;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands;

public sealed class SubmitReviewRequestCommandHandlerTests
{
    private readonly ICaptureRepository _captures = Substitute.For<ICaptureRepository>();
    private readonly IReviewRequestRepository _reviewRequests = Substitute.For<IReviewRequestRepository>();
    private readonly IAuditLogRepository _auditLog = Substitute.For<IAuditLogRepository>();
    private readonly SubmitReviewRequestCommandHandler _sut;
    private readonly CaptureId _captureId;

    public SubmitReviewRequestCommandHandlerTests()
    {
        var capture = Capture.Create(
            ExamId.New(), StudentId.New(), DeviceId.New(),
            new PageNumber(1), Hash.FromHex(new string('a', 64)),
            new Signature(new byte[64]));
        _captureId = capture.Id;
        _captures.GetByIdAsync(_captureId, Arg.Any<CancellationToken>()).Returns(capture);
        _captures.GetByIdAsync(Arg.Is<CaptureId>(id => id != _captureId), Arg.Any<CancellationToken>())
            .Returns((Capture?)null);
        _sut = new SubmitReviewRequestCommandHandler(_captures, _reviewRequests, _auditLog);
    }

    [Fact]
    public async Task Handle_WithValidRequest_ReturnNonEmptyId()
    {
        var result = await _sut.Handle(
            new SubmitReviewRequestCommand(_captureId.Value, Guid.NewGuid(), "Answers misread by OCR"), default);

        result.ReviewRequestId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Handle_WithValidRequest_PersistsReviewRequest()
    {
        await _sut.Handle(
            new SubmitReviewRequestCommand(_captureId.Value, Guid.NewGuid(), "Ink smudge on question 3"), default);

        await _reviewRequests.Received(1).AddAsync(
            Arg.Is<ReviewRequest>(rr => rr.Status == ReviewRequestStatus.Pending),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithValidRequest_AppendsAuditEntry()
    {
        await _sut.Handle(
            new SubmitReviewRequestCommand(_captureId.Value, Guid.NewGuid(), "Wrong answer recorded"), default);

        await _auditLog.Received(1).AppendAsync(
            Arg.Is<AuditLog>(e => e.Action == AuditAction.ReviewRequestSubmitted),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCaptureNotFound_ThrowsKeyNotFoundException()
    {
        await Assert.ThrowsAsync<KeyNotFoundException>(() =>
            _sut.Handle(
                new SubmitReviewRequestCommand(Guid.NewGuid(), Guid.NewGuid(), "Some reason"), default));
    }

    [Fact]
    public async Task Handle_WithEmptyReason_ThrowsArgumentException()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            _sut.Handle(
                new SubmitReviewRequestCommand(_captureId.Value, Guid.NewGuid(), ""), default));
    }
}
