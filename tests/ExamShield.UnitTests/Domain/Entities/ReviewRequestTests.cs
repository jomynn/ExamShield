using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;

namespace ExamShield.UnitTests.Domain.Entities;

public sealed class ReviewRequestTests
{
    private static ReviewRequest MakePending() =>
        ReviewRequest.Submit(StudentId.New(), CaptureId.New(), "Answer looks wrong");

    // ── Submit ────────────────────────────────────────────────────────────────

    [Fact]
    public void Submit_ValidArgs_SetsAllProperties()
    {
        var studentId = StudentId.New();
        var captureId = CaptureId.New();

        var req = ReviewRequest.Submit(studentId, captureId, "My reason");

        req.Id.Value.Should().NotBe(Guid.Empty);
        req.StudentId.Should().Be(studentId);
        req.CaptureId.Should().Be(captureId);
        req.Reason.Should().Be("My reason");
        req.Status.Should().Be(ReviewRequestStatus.Pending);
        req.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Submit_NullStudentId_ThrowsArgumentNullException()
    {
        var act = () => ReviewRequest.Submit(null!, CaptureId.New(), "reason");
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Submit_NullCaptureId_ThrowsArgumentNullException()
    {
        var act = () => ReviewRequest.Submit(StudentId.New(), null!, "reason");
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Submit_EmptyReason_ThrowsArgumentException(string reason)
    {
        var act = () => ReviewRequest.Submit(StudentId.New(), CaptureId.New(), reason);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Submit_TrimsReason()
    {
        var req = ReviewRequest.Submit(StudentId.New(), CaptureId.New(), "  spaced  ");
        req.Reason.Should().Be("spaced");
    }

    [Fact]
    public void Submit_GeneratesUniqueIds()
    {
        MakePending().Id.Should().NotBe(MakePending().Id);
    }

    // ── Resolve ───────────────────────────────────────────────────────────────

    [Fact]
    public void Resolve_PendingRequest_SetsStatusResolvedAndNote()
    {
        var req = MakePending();
        req.Resolve("Score confirmed correct");

        req.Status.Should().Be(ReviewRequestStatus.Resolved);
        req.ResolutionNote.Should().Be("Score confirmed correct");
    }

    [Fact]
    public void Resolve_AlreadyResolved_ThrowsInvalidOperation()
    {
        var req = MakePending();
        req.Resolve("note");
        req.Invoking(r => r.Resolve("again")).Should().Throw<InvalidOperationException>().WithMessage("*closed*");
    }

    [Fact]
    public void Resolve_AlreadyRejected_ThrowsInvalidOperation()
    {
        var req = MakePending();
        req.Reject("not valid");
        req.Invoking(r => r.Resolve("note")).Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Resolve_EmptyNote_ThrowsArgumentException(string note)
    {
        var req = MakePending();
        var act = () => req.Resolve(note);
        act.Should().Throw<ArgumentException>();
    }

    // ── Reject ────────────────────────────────────────────────────────────────

    [Fact]
    public void Reject_PendingRequest_SetsStatusRejected()
    {
        var req = MakePending();
        req.Reject("Invalid claim");

        req.Status.Should().Be(ReviewRequestStatus.Rejected);
        req.ResolutionNote.Should().Be("Invalid claim");
    }

    [Fact]
    public void Reject_AlreadyRejected_ThrowsInvalidOperation()
    {
        var req = MakePending();
        req.Reject("reason");
        req.Invoking(r => r.Reject("again")).Should().Throw<InvalidOperationException>().WithMessage("*closed*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_EmptyReason_ThrowsArgumentException(string reason)
    {
        var req = MakePending();
        var act = () => req.Reject(reason);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Reject_TrimsResolutionNote()
    {
        var req = MakePending();
        req.Reject("  trimmed  ");
        req.ResolutionNote.Should().Be("trimmed");
    }
}
