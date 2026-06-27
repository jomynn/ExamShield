using ExamShield.Domain.ValueObjects;

namespace ExamShield.Domain.Entities;

public sealed class ExamCandidate : AggregateRoot
{
    public ExamId ExamId { get; private set; } = null!;
    public StudentId StudentId { get; private set; } = null!;
    public DateTimeOffset EnrolledAt { get; private set; }

    private ExamCandidate() { }

    public static ExamCandidate Enroll(ExamId examId, StudentId studentId)
    {
        ArgumentNullException.ThrowIfNull(examId);
        ArgumentNullException.ThrowIfNull(studentId);
        return new ExamCandidate
        {
            ExamId = examId,
            StudentId = studentId,
            EnrolledAt = DateTimeOffset.UtcNow
        };
    }
}
