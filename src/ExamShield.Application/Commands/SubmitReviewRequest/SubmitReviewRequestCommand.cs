using MediatR;

namespace ExamShield.Application.Commands.SubmitReviewRequest;

public sealed record SubmitReviewRequestResult(Guid ReviewRequestId);

public sealed record SubmitReviewRequestCommand(
    Guid CaptureId,
    Guid StudentId,
    string Reason) : IRequest<SubmitReviewRequestResult>;
