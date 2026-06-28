using ExamShield.Domain.Entities;
using FluentAssertions;

namespace ExamShield.UnitTests.Domain.Entities;

public sealed class ExamTests
{
    // ── Create ────────────────────────────────────────────────────────────────

    [Fact]
    public void Create_ValidArgs_SetsProperties()
    {
        var exam = Exam.Create("Math Final", "description", 50);

        exam.Id.Value.Should().NotBe(Guid.Empty);
        exam.Name.Should().Be("Math Final");
        exam.Description.Should().Be("description");
        exam.TotalQuestions.Should().Be(50);
        exam.Status.Should().Be(ExamStatus.Draft);
        exam.IsDeleted.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyName_ThrowsArgumentException(string name)
    {
        var act = () => Exam.Create(name, null, 10);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_ZeroOrNegativeTotalQuestions_ThrowsArgumentOutOfRange(int total)
    {
        var act = () => Exam.Create("Exam", null, total);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_EndsAtBeforeScheduledAt_ThrowsArgumentException()
    {
        var now = DateTimeOffset.UtcNow;
        var act = () => Exam.Create("Exam", null, 10, scheduledAt: now.AddHours(2), endsAt: now.AddHours(1));
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    public void Create_ZeroOrNegativeMaxCandidates_ThrowsArgumentOutOfRange(int max)
    {
        var act = () => Exam.Create("Exam", null, 10, maxCandidates: max);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        Exam.Create("E1", null, 5).Id.Should().NotBe(Exam.Create("E2", null, 5).Id);
    }

    // ── Update ────────────────────────────────────────────────────────────────

    [Fact]
    public void Update_DraftExam_UpdatesFields()
    {
        var exam = Exam.Create("Old Name", null, 10);
        var sched = DateTimeOffset.UtcNow.AddDays(1);
        var ends = sched.AddHours(2);

        exam.Update("New Name", "New Desc", sched, ends);

        exam.Name.Should().Be("New Name");
        exam.Description.Should().Be("New Desc");
        exam.ScheduledAt.Should().Be(sched);
        exam.EndsAt.Should().Be(ends);
    }

    [Fact]
    public void Update_ActiveExam_ThrowsInvalidOperation()
    {
        var exam = Exam.Create("Exam", null, 10);
        exam.Activate();

        var act = () => exam.Update("X", null, null, null);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Draft*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Update_EmptyName_ThrowsArgumentException(string name)
    {
        var exam = Exam.Create("Exam", null, 10);
        var act = () => exam.Update(name, null, null, null);
        act.Should().Throw<ArgumentException>();
    }

    // ── Activate ─────────────────────────────────────────────────────────────

    [Fact]
    public void Activate_DraftExam_SetsStatusActive()
    {
        var exam = Exam.Create("Exam", null, 10);
        exam.Activate();
        exam.Status.Should().Be(ExamStatus.Active);
    }

    [Fact]
    public void Activate_AlreadyActive_ThrowsInvalidOperation()
    {
        var exam = Exam.Create("Exam", null, 10);
        exam.Activate();
        exam.Invoking(e => e.Activate()).Should().Throw<InvalidOperationException>();
    }

    // ── Close ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Close_ActiveExam_SetsStatusClosed()
    {
        var exam = Exam.Create("Exam", null, 10);
        exam.Activate();
        exam.Close();
        exam.Status.Should().Be(ExamStatus.Closed);
    }

    [Fact]
    public void Close_AlreadyClosed_ThrowsInvalidOperation()
    {
        var exam = Exam.Create("Exam", null, 10);
        exam.Activate();
        exam.Close();
        exam.Invoking(e => e.Close()).Should().Throw<InvalidOperationException>().WithMessage("*closed*");
    }

    // ── MarkDeleted ───────────────────────────────────────────────────────────

    [Fact]
    public void MarkDeleted_DraftExam_SetsIsDeletedTrue()
    {
        var exam = Exam.Create("Exam", null, 10);
        exam.MarkDeleted();
        exam.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public void MarkDeleted_ActiveExam_ThrowsInvalidOperation()
    {
        var exam = Exam.Create("Exam", null, 10);
        exam.Activate();
        exam.Invoking(e => e.MarkDeleted()).Should().Throw<InvalidOperationException>().WithMessage("*Draft*");
    }

    [Fact]
    public void MarkDeleted_AlreadyDeleted_ThrowsInvalidOperation()
    {
        var exam = Exam.Create("Exam", null, 10);
        exam.MarkDeleted();
        exam.Invoking(e => e.MarkDeleted()).Should().Throw<InvalidOperationException>().WithMessage("*already deleted*");
    }
}
