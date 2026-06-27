namespace ExamShield.Domain.Enums;

public enum SecurityEventType
{
    HashMismatch,
    InvalidSignature,
    DuplicateUpload,
    UnauthorizedAccess,
    SuspiciousLogin,
    DeviceBlacklisted,
    LoginSuccess,
    LoginFailed,
    WatermarkTampered,
}
