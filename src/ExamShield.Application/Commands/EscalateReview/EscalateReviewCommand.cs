using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.EscalateReview;

public sealed record EscalateReviewCommand(Guid ReviewId, Guid SupervisorId, string Reason) : IRequest;

public sealed class EscalateReviewCommandHandler(
    IManualReviewRepository reviews,
    IAuditLogRepository auditLog)
    : IRequestHandler<EscalateReviewCommand>
{
    public async Task Handle(EscalateReviewCommand command, CancellationToken ct)
    {
        var review = await reviews.GetByIdAsync(new ManualReviewId(command.ReviewId), ct)
            ?? throw new ManualReviewNotFoundException(command.ReviewId);

        review.Escalate(command.Reason, new UserId(command.SupervisorId));
        await reviews.UpdateAsync(review, ct);
        await auditLog.AppendAsync(AuditLog.Record(AuditAction.ReviewEscalated), ct);
    }
}
