namespace ExamShield.Domain.Exceptions;

public sealed class StudentAlreadyEnrolledException(Guid examId, Guid studentId)
    : Exception($"Student {studentId} is already enrolled in exam {examId}.");
