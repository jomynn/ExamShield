using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.ChangePassword;

public sealed class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IRefreshTokenRepository _refreshTokens;

    public ChangePasswordCommandHandler(
        IUserRepository users, IPasswordHasher hasher, IRefreshTokenRepository refreshTokens)
    {
        _users = users; _hasher = hasher; _refreshTokens = refreshTokens;
    }

    public async Task Handle(ChangePasswordCommand command, CancellationToken ct)
    {
        var newPassword = new Password(command.NewPassword);

        var user = await _users.GetByIdAsync(new UserId(command.UserId), ct);

        if (user is null || !_hasher.Verify(command.CurrentPassword, user.PasswordHash))
            throw new InvalidCredentialsException();

        user.ChangePassword(_hasher.Hash(newPassword.Value));
        await _users.SaveAsync(user, ct);
        await _refreshTokens.RevokeAllForUserAsync(user.Id, ct);
    }
}
