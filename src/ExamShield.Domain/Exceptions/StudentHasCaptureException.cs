namespace ExamShield.Domain.Exceptions;

public sealed class StudentHasCaptureException(Guid examId, Guid studentId)
    : Exception($"Student {studentId} has existing captures in exam {examId} and cannot be unenrolled.");
