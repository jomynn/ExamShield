using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Queries.GetExamSubmissionStatus;

public sealed record StudentSubmissionStatus(Guid StudentId, bool HasSubmitted, string? CaptureStatus);

public sealed record GetExamSubmissionStatusResult(
    Guid ExamId,
    int TotalEnrolled,
    int Submitted,
    int Missing,
    IReadOnlyList<StudentSubmissionStatus> Students);

public sealed record GetExamSubmissionStatusQuery(Guid ExamId) : IRequest<GetExamSubmissionStatusResult>;

public sealed class GetExamSubmissionStatusQueryHandler(
    IExamCandidateRepository candidates,
    ICaptureRepository captures) : IRequestHandler<GetExamSubmissionStatusQuery, GetExamSubmissionStatusResult>
{
    public async Task<GetExamSubmissionStatusResult> Handle(
        GetExamSubmissionStatusQuery query, CancellationToken ct)
    {
        var examId   = new ExamId(query.ExamId);
        var enrolled = await candidates.ListByExamIdAsync(examId, ct);
        var allCaptures = await captures.ListByExamIdAsync(examId, ct);

        // Index most-recent capture status per enrolled student
        var captureByStudent = allCaptures
            .GroupBy(c => c.StudentId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.CapturedAt).First());

        var students = enrolled.Select(c =>
        {
            var hasCapture = captureByStudent.TryGetValue(c.StudentId, out var capture);
            return new StudentSubmissionStatus(
                c.StudentId.Value,
                hasCapture,
                hasCapture ? capture!.Status.ToString() : null);
        }).ToList();

        var submitted = students.Count(s => s.HasSubmitted);
        return new GetExamSubmissionStatusResult(
            query.ExamId,
            enrolled.Count,
            submitted,
            enrolled.Count - submitted,
            students);
    }
}
