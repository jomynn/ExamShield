using ExamShield.Application.Commands.SubmitReviewRequest;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.SubmitReviewRequest;

public sealed class SubmitReviewRequestOwnershipTests
{
    private readonly ICaptureRepository        _captures      = Substitute.For<ICaptureRepository>();
    private readonly IReviewRequestRepository  _requests      = Substitute.For<IReviewRequestRepository>();
    private readonly IAuditLogRepository       _auditLog      = Substitute.For<IAuditLogRepository>();
    private readonly SubmitReviewRequestCommandHandler _sut;

    public SubmitReviewRequestOwnershipTests() =>
        _sut = new SubmitReviewRequestCommandHandler(_captures, _requests, _auditLog);

    private static Capture MakeCapture(StudentId owner)
    {
        return Capture.Create(
            new ExamId(Guid.NewGuid()), owner, new DeviceId(Guid.NewGuid()),
            new PageNumber(1), Hash.FromHex(new string('a', 64)), new Signature(new byte[64]));
    }

    [Fact]
    public async Task Handle_WhenStudentIdMatchesCapture_Succeeds()
    {
        var student = new StudentId(Guid.NewGuid());
        var capture = MakeCapture(student);
        _captures.GetByIdAsync(Arg.Any<CaptureId>(), default).Returns(capture);

        var cmd = new SubmitReviewRequestCommand(capture.Id.Value, student.Value, "My answers look wrong");
        var act = () => _sut.Handle(cmd, default);

        await act.Should().NotThrowAsync();
        await _requests.Received(1).AddAsync(Arg.Any<ReviewRequest>(), default);
    }

    [Fact]
    public async Task Handle_WhenStudentIdDoesNotMatchCapture_ThrowsUnauthorized()
    {
        var owner   = new StudentId(Guid.NewGuid());
        var other   = new StudentId(Guid.NewGuid());
        var capture = MakeCapture(owner);
        _captures.GetByIdAsync(Arg.Any<CaptureId>(), default).Returns(capture);

        var cmd = new SubmitReviewRequestCommand(capture.Id.Value, other.Value, "Trying to review someone else");
        var act = () => _sut.Handle(cmd, default);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        await _requests.DidNotReceive().AddAsync(Arg.Any<ReviewRequest>(), default);
    }
}
