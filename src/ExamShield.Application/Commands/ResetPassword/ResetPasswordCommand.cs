using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.ResetPassword;

public sealed record ResetPasswordCommand(string Token, string NewPassword) : IRequest;

public sealed class ResetPasswordCommandHandler(
    IPasswordResetTokenRepository tokens,
    IUserRepository users,
    IPasswordHasher hasher,
    IRefreshTokenRepository refreshTokens,
    IAuditLogRepository auditLog)
    : IRequestHandler<ResetPasswordCommand>
{
    public async Task Handle(ResetPasswordCommand command, CancellationToken ct)
    {
        var token = await tokens.FindAsync(command.Token, ct)
            ?? throw new InvalidResetTokenException();

        if (!token.IsValid)
            throw new InvalidResetTokenException();

        var user = await users.FindByEmailAsync(new Email(token.Email), ct)
            ?? throw new InvalidResetTokenException();

        var validatedPassword = new Password(command.NewPassword);
        user.ChangePassword(hasher.Hash(validatedPassword.Value));
        token.MarkUsed();

        await users.SaveAsync(user, ct);
        await tokens.UpdateAsync(token, ct);
        await refreshTokens.RevokeAllForUserAsync(user.Id, ct);
        await auditLog.AppendAsync(AuditLog.Record(AuditAction.PasswordReset), ct);
    }
}
