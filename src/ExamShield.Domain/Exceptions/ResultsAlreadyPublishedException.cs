namespace ExamShield.Domain.Exceptions;

public sealed class ResultsAlreadyPublishedException(Guid examId)
    : Exception($"Results for exam {examId} have already been published.");
