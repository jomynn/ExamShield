namespace ExamShield.Domain.Exceptions;

public sealed class ExamFullException(Guid examId, int maxCandidates)
    : Exception($"Exam {examId} has reached its maximum capacity of {maxCandidates} candidates.");
