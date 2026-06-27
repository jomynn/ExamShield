namespace ExamShield.Domain.Enums;

public enum AuditAction
{
    UserCreated,
    DeviceRegistered,
    CaptureRegistered,
    ImageUploaded,
    HashVerified,
    TamperingDetected,
    ManualReviewStarted,
    ManualReviewCompleted,
    OCRStarted,
    OCRCompleted,
    ScoreGenerated,
    ResultPublished,
    ResultsPublished,
    ReviewRequestSubmitted,
    AnswerKeySet
}
