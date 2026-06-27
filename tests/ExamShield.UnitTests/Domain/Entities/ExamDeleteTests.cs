using ExamShield.Domain.Entities;

namespace ExamShield.UnitTests.Domain.Entities;

public sealed class ExamDeleteTests
{
    private static Exam Draft() => Exam.Create("Delete Me", null, 10);

    [Fact]
    public void MarkDeleted_DraftExam_SetsIsDeleted()
    {
        var exam = Draft();
        exam.MarkDeleted();
        Assert.True(exam.IsDeleted);
    }

    [Fact]
    public void MarkDeleted_ActiveExam_Throws()
    {
        var exam = Draft();
        exam.Activate();
        Assert.Throws<InvalidOperationException>(() => exam.MarkDeleted());
    }

    [Fact]
    public void MarkDeleted_ClosedExam_Throws()
    {
        var exam = Draft();
        exam.Activate();
        exam.Close();
        Assert.Throws<InvalidOperationException>(() => exam.MarkDeleted());
    }

    [Fact]
    public void MarkDeleted_AlreadyDeleted_Throws()
    {
        var exam = Draft();
        exam.MarkDeleted();
        Assert.Throws<InvalidOperationException>(() => exam.MarkDeleted());
    }
}
