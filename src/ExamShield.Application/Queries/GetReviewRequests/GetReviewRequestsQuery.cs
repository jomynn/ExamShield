using MediatR;

namespace ExamShield.Application.Queries.GetReviewRequests;

public sealed record ReviewRequestDto(
    Guid ReviewRequestId, Guid StudentId, Guid CaptureId,
    string Reason, string Status, DateTimeOffset CreatedAt);

public sealed record GetReviewRequestsResult(IReadOnlyList<ReviewRequestDto> Items);

public sealed record GetReviewRequestsQuery(Guid StudentId) : IRequest<GetReviewRequestsResult>;
