namespace ExamShield.Api.Contracts;

public sealed record SubmitReviewRequestBody(Guid CaptureId, Guid StudentId, string Reason);
public sealed record SubmitReviewRequestResponse(Guid ReviewRequestId);

public sealed record ReviewRequestItemResponse(
    Guid ReviewRequestId, Guid StudentId, Guid CaptureId,
    string Reason, string Status, DateTimeOffset CreatedAt);

public sealed record ReviewRequestListResponse(IReadOnlyList<ReviewRequestItemResponse> Items);

public sealed record StudentResultItemResponse(
    Guid ScoreId, Guid CaptureId, Guid ExamId, string ExamName,
    int CorrectAnswers, int TotalQuestions, double Percentage,
    DateTimeOffset ScoredAt, string HashHex, bool IsVerified);

public sealed record StudentResultsResponse(
    Guid StudentId,
    IReadOnlyList<StudentResultItemResponse> Results);
