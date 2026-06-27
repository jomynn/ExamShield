namespace ExamShield.Domain.Exceptions;

public sealed class AnswerKeyAlreadySetException(Guid examId)
    : Exception($"An answer key for exam {examId} has already been set and cannot be changed.");
