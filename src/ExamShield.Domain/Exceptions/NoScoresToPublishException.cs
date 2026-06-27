namespace ExamShield.Domain.Exceptions;

public sealed class NoScoresToPublishException(Guid examId)
    : Exception($"Exam {examId} has no scores to publish.");
