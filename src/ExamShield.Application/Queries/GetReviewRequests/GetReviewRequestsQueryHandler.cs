using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Queries.GetReviewRequests;

public sealed class GetReviewRequestsQueryHandler(IReviewRequestRepository repository)
    : IRequestHandler<GetReviewRequestsQuery, GetReviewRequestsResult>
{
    public async Task<GetReviewRequestsResult> Handle(
        GetReviewRequestsQuery query, CancellationToken ct)
    {
        var studentId = new StudentId(query.StudentId);
        var items = await repository.ListByStudentAsync(studentId, ct);
        var dtos = items
            .Select(r => new ReviewRequestDto(
                r.Id.Value, r.StudentId.Value, r.CaptureId.Value,
                r.Reason, r.Status.ToString(), r.CreatedAt))
            .ToList();
        return new GetReviewRequestsResult(dtos);
    }
}
