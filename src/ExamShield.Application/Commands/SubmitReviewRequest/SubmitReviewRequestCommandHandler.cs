using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.SubmitReviewRequest;

public sealed class SubmitReviewRequestCommandHandler(
    ICaptureRepository captures,
    IReviewRequestRepository reviewRequests,
    IAuditLogRepository auditLog)
    : IRequestHandler<SubmitReviewRequestCommand, SubmitReviewRequestResult>
{
    public async Task<SubmitReviewRequestResult> Handle(
        SubmitReviewRequestCommand command, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(command.Reason))
            throw new ArgumentException("Reason cannot be empty.", nameof(command.Reason));

        var captureId = new CaptureId(command.CaptureId);
        var studentId = new StudentId(command.StudentId);

        var capture = await captures.GetByIdAsync(captureId, ct)
            ?? throw new KeyNotFoundException($"Capture '{command.CaptureId}' not found.");

        var request = ReviewRequest.Submit(studentId, captureId, command.Reason);

        await reviewRequests.AddAsync(request, ct);
        await auditLog.AppendAsync(
            AuditLog.Record(AuditAction.ReviewRequestSubmitted, captureId: capture.Id), ct);

        return new SubmitReviewRequestResult(request.Id.Value);
    }
}
