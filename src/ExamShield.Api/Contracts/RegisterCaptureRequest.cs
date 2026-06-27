namespace ExamShield.Api.Contracts;

public sealed record RegisterCaptureRequest(
    Guid ExamId,
    Guid StudentId,
    Guid DeviceId,
    int PageNumber,
    string HashHex,
    byte[] SignatureBytes
);

public sealed record RegisterCaptureResponse(Guid CaptureId);

public sealed record CaptureDetailResponse(
    Guid CaptureId, Guid ExamId, Guid StudentId, Guid DeviceId,
    int PageNumber, string Hash, string Signature,
    string Status, DateTimeOffset CapturedAt, string? StorageKey);
