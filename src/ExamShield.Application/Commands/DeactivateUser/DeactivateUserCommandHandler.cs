using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.DeactivateUser;

public sealed class DeactivateUserCommandHandler(
    IUserRepository users,
    IRefreshTokenRepository tokens,
    IAuditLogRepository auditLog)
    : IRequestHandler<DeactivateUserCommand>
{
    public async Task Handle(DeactivateUserCommand request, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(new UserId(request.UserId), ct)
            ?? throw new UserNotFoundException(request.UserId);

        user.Deactivate();
        await users.SaveAsync(user, ct);
        await tokens.RevokeAllForUserAsync(user.Id, ct);
        await auditLog.AppendAsync(AuditLog.Record(AuditAction.UserDeactivated), ct);
    }
}
