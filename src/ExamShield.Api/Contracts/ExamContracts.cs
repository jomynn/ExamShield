namespace ExamShield.Api.Contracts;

public sealed record CreateExamRequest(
    string Name, string? Description, int TotalQuestions,
    DateTimeOffset? ScheduledAt = null, DateTimeOffset? EndsAt = null);

public sealed record SetAnswerKeyRequest(IReadOnlyDictionary<int, string> Answers);
public sealed record AnswerKeyResponse(Guid ExamId, IReadOnlyDictionary<string, string> Answers, DateTimeOffset CreatedAt);

public sealed record ExamResponse(
    Guid ExamId, string Name, string? Description,
    string Status, int TotalQuestions, DateTimeOffset CreatedAt,
    DateTimeOffset? ScheduledAt, DateTimeOffset? EndsAt);

public sealed record UpdateExamRequest(
    string Name, string? Description,
    DateTimeOffset? ScheduledAt, DateTimeOffset? EndsAt);

public sealed record EnrollStudentRequest(Guid StudentId);
public sealed record BulkEnrollRequest(IReadOnlyList<Guid> StudentIds);
public sealed record BulkEnrollResponse(int Enrolled, int AlreadyEnrolled, int Total);

public sealed record StudentSubmissionItem(Guid StudentId, bool HasSubmitted, string? CaptureStatus);
public sealed record ExamSubmissionStatusResponse(
    Guid ExamId, int TotalEnrolled, int Submitted, int Missing,
    IReadOnlyList<StudentSubmissionItem> Students);
public sealed record ExamCandidateItem(Guid StudentId, DateTimeOffset EnrolledAt);
public sealed record ExamCandidatesResponse(Guid ExamId, IReadOnlyList<ExamCandidateItem> Candidates);

public sealed record ExamListResponse(
    IReadOnlyList<ExamResponse> Exams,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages);
