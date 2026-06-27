using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;

namespace ExamShield.UnitTests.Domain.Entities;

public sealed class ExamUpdateTests
{
    private static Exam Draft() => Exam.Create("Original", "Desc", 10);

    [Fact]
    public void Update_DraftExam_ChangesName()
    {
        var exam = Draft();
        exam.Update("Renamed", "New desc", null, null);
        Assert.Equal("Renamed", exam.Name);
    }

    [Fact]
    public void Update_DraftExam_ChangesScheduledAt()
    {
        var exam = Draft();
        var t    = DateTimeOffset.UtcNow.AddDays(1);
        exam.Update("Original", null, t, null);
        Assert.Equal(t, exam.ScheduledAt);
    }

    [Fact]
    public void Update_EndsAtBeforeScheduledAt_Throws()
    {
        var exam = Draft();
        var t    = DateTimeOffset.UtcNow.AddDays(1);
        Assert.Throws<ArgumentException>(() => exam.Update("X", null, t.AddDays(2), t));
    }

    [Fact]
    public void Update_EmptyName_Throws()
    {
        var exam = Draft();
        Assert.Throws<ArgumentException>(() => exam.Update("  ", null, null, null));
    }

    [Fact]
    public void Update_ActiveExam_ThrowsInvalidOperation()
    {
        var exam = Draft();
        exam.Activate();
        Assert.Throws<InvalidOperationException>(() => exam.Update("X", null, null, null));
    }

    [Fact]
    public void Update_ClosedExam_ThrowsInvalidOperation()
    {
        var exam = Draft();
        exam.Activate();
        exam.Close();
        Assert.Throws<InvalidOperationException>(() => exam.Update("X", null, null, null));
    }
}
