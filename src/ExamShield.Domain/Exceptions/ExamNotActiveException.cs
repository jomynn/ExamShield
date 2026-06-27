namespace ExamShield.Domain.Exceptions;

public sealed class ExamNotActiveException : Exception
{
    public ExamNotActiveException(Guid examId)
        : base($"Exam '{examId}' is not active. Captures can only be registered for active exams.") { }
}
