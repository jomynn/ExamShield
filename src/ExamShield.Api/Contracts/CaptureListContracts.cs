namespace ExamShield.Api.Contracts;

public sealed record CaptureListItem(
    Guid CaptureId, Guid ExamId, Guid StudentId, Guid DeviceId,
    string Status, DateTimeOffset CapturedAt, string? StorageKey);

public sealed record CaptureListResponse(
    IReadOnlyList<CaptureListItem> Captures,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
