namespace ExamShield.Domain.Exceptions;

public sealed class DuplicateScoreException(Guid captureId)
    : Exception($"A score already exists for capture {captureId}.");
