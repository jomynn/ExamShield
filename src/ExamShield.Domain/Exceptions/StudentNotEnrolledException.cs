namespace ExamShield.Domain.Exceptions;

public sealed class StudentNotEnrolledException(Guid examId, Guid studentId)
    : Exception($"Student {studentId} is not enrolled in exam {examId}.");
