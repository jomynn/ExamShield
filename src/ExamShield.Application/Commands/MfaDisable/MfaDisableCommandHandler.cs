using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.MfaDisable;

public sealed class MfaDisableCommandHandler(
    IUserRepository users,
    IAuditLogRepository auditLog)
    : IRequestHandler<MfaDisableCommand, MfaDisableResult>
{
    public async Task<MfaDisableResult> Handle(MfaDisableCommand cmd, CancellationToken ct)
    {
        var user = await users.GetByIdAsync(new UserId(cmd.UserId), ct)
            ?? throw new InvalidOperationException("User not found.");

        user.DisableMfa();
        await users.SaveAsync(user, ct);
        await auditLog.AppendAsync(AuditLog.Record(AuditAction.MfaDisabled), ct);
        return new MfaDisableResult(false);
    }
}
