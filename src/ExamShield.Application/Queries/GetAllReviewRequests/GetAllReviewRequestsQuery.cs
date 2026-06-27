using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using MediatR;

namespace ExamShield.Application.Queries.GetAllReviewRequests;

public sealed record AllReviewRequestDto(
    Guid ReviewRequestId, Guid StudentId, Guid CaptureId,
    string Reason, string Status, string? ResolutionNote, DateTimeOffset CreatedAt);

public sealed record GetAllReviewRequestsResult(IReadOnlyList<AllReviewRequestDto> Items);

public sealed record GetAllReviewRequestsQuery(string? Status) : IRequest<GetAllReviewRequestsResult>;

public sealed class GetAllReviewRequestsQueryHandler(IReviewRequestRepository repo)
    : IRequestHandler<GetAllReviewRequestsQuery, GetAllReviewRequestsResult>
{
    public async Task<GetAllReviewRequestsResult> Handle(
        GetAllReviewRequestsQuery query, CancellationToken ct)
    {
        ReviewRequestStatus? filter = query.Status is not null
            && Enum.TryParse<ReviewRequestStatus>(query.Status, ignoreCase: true, out var s)
            ? s : null;

        var items = await repo.ListAllAsync(filter, ct);
        var dtos = items
            .Select(r => new AllReviewRequestDto(
                r.Id.Value, r.StudentId.Value, r.CaptureId.Value,
                r.Reason, r.Status.ToString(), r.ResolutionNote, r.CreatedAt))
            .ToList();
        return new GetAllReviewRequestsResult(dtos);
    }
}
