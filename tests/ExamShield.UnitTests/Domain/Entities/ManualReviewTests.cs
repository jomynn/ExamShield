using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;

namespace ExamShield.UnitTests.Domain.Entities;

public sealed class ManualReviewTests
{
    private static OcrResult MakeLowConfidenceOcr() =>
        OcrResult.Create(CaptureId.New(), [new ExtractedAnswer(1, "A", new OcrConfidence(0.3))]);

    private static ManualReview MakePendingReview() =>
        ManualReview.CreateFor(MakeLowConfidenceOcr());

    private static ManualReview MakeCompletedReview()
    {
        var r = MakePendingReview();
        r.Complete([new ReviewedAnswer(1, "B")], UserId.New());
        return r;
    }

    // ── CreateFor ─────────────────────────────────────────────────────────────

    [Fact]
    public void CreateFor_SetsAllProperties()
    {
        var ocr = MakeLowConfidenceOcr();
        var review = ManualReview.CreateFor(ocr);

        review.Id.Value.Should().NotBe(Guid.Empty);
        review.OcrResultId.Should().Be(ocr.Id);
        review.CaptureId.Should().Be(ocr.CaptureId);
        review.Status.Should().Be(ManualReviewStatus.Pending);
        review.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void CreateFor_NullOcrResult_ThrowsArgumentNullException()
    {
        var act = () => ManualReview.CreateFor(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void CreateFor_GeneratesUniqueIds()
    {
        var a = ManualReview.CreateFor(MakeLowConfidenceOcr());
        var b = ManualReview.CreateFor(MakeLowConfidenceOcr());
        a.Id.Should().NotBe(b.Id);
    }

    // ── Complete ──────────────────────────────────────────────────────────────

    [Fact]
    public void Complete_PendingReview_SetsStatusAndAnswers()
    {
        var review = MakePendingReview();
        var reviewer = UserId.New();

        review.Complete([new ReviewedAnswer(1, "C")], reviewer);

        review.Status.Should().Be(ManualReviewStatus.Completed);
        review.ReviewedAnswers.Should().ContainSingle();
        review.ReviewedBy.Should().Be(reviewer);
        review.CompletedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Complete_AlreadyCompleted_ThrowsInvalidOperation()
    {
        var review = MakeCompletedReview();
        review.Invoking(r => r.Complete([new ReviewedAnswer(1, "A")], UserId.New()))
            .Should().Throw<InvalidOperationException>().WithMessage("*pending*");
    }

    [Fact]
    public void Complete_EmptyAnswers_ThrowsArgumentException()
    {
        var review = MakePendingReview();
        var act = () => review.Complete([], UserId.New());
        act.Should().Throw<ArgumentException>().WithMessage("*at least one*");
    }

    [Fact]
    public void Complete_NullReviewedBy_ThrowsArgumentNullException()
    {
        var review = MakePendingReview();
        var act = () => review.Complete([new ReviewedAnswer(1, "A")], null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ── Approve ───────────────────────────────────────────────────────────────

    [Fact]
    public void Approve_CompletedReview_SetsStatusApproved()
    {
        var review = MakeCompletedReview();
        review.Approve(UserId.New());
        review.Status.Should().Be(ManualReviewStatus.Approved);
    }

    [Fact]
    public void Approve_PendingReview_ThrowsInvalidOperation()
    {
        var review = MakePendingReview();
        review.Invoking(r => r.Approve(UserId.New()))
            .Should().Throw<InvalidOperationException>().WithMessage("*completed*");
    }

    // ── Reject ────────────────────────────────────────────────────────────────

    [Fact]
    public void Reject_CompletedReview_SetsStatusRejectedAndStoresReason()
    {
        var review = MakeCompletedReview();
        review.Reject("Illegible handwriting", UserId.New());

        review.Status.Should().Be(ManualReviewStatus.Rejected);
        review.RejectionReason.Should().Be("Illegible handwriting");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Reject_EmptyReason_ThrowsArgumentException(string reason)
    {
        var review = MakeCompletedReview();
        var act = () => review.Reject(reason, UserId.New());
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Reject_PendingReview_ThrowsInvalidOperation()
    {
        var review = MakePendingReview();
        review.Invoking(r => r.Reject("reason", UserId.New()))
            .Should().Throw<InvalidOperationException>();
    }

    // ── Escalate ──────────────────────────────────────────────────────────────

    [Fact]
    public void Escalate_CompletedReview_SetsStatusEscalated()
    {
        var review = MakeCompletedReview();
        review.Escalate("Needs senior review", UserId.New());
        review.Status.Should().Be(ManualReviewStatus.Escalated);
        review.EscalationReason.Should().Be("Needs senior review");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Escalate_EmptyReason_ThrowsArgumentException(string reason)
    {
        var review = MakeCompletedReview();
        var act = () => review.Escalate(reason, UserId.New());
        act.Should().Throw<ArgumentException>();
    }
}
