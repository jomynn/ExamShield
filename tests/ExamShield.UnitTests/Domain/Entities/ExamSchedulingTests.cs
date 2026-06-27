using ExamShield.Domain.Entities;

namespace ExamShield.UnitTests.Domain.Entities;

public sealed class ExamSchedulingTests
{
    [Fact]
    public void Create_WithoutSchedule_HasNullSchedule()
    {
        var exam = Exam.Create("Final", null, 40);

        Assert.Null(exam.ScheduledAt);
        Assert.Null(exam.EndsAt);
    }

    [Fact]
    public void Create_WithSchedule_StoresSchedule()
    {
        var start = DateTimeOffset.UtcNow.AddDays(7);
        var end   = start.AddHours(3);

        var exam = Exam.Create("Final", null, 40, start, end);

        Assert.Equal(start, exam.ScheduledAt);
        Assert.Equal(end,   exam.EndsAt);
    }

    [Fact]
    public void Create_WhenEndsAtBeforeScheduledAt_ThrowsArgumentException()
    {
        var start = DateTimeOffset.UtcNow.AddDays(7);
        var end   = start.AddHours(-1); // before start

        Assert.Throws<ArgumentException>(
            () => Exam.Create("Final", null, 40, start, end));
    }

    [Fact]
    public void Create_WithScheduledAtOnly_IsAllowed()
    {
        var start = DateTimeOffset.UtcNow.AddDays(1);

        var exam = Exam.Create("Midterm", null, 20, start, null);

        Assert.Equal(start, exam.ScheduledAt);
        Assert.Null(exam.EndsAt);
    }
}
