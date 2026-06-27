using ExamShield.Application.Commands.Login;
using ExamShield.Application.Interfaces;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.MfaLogin;

public sealed class MfaLoginCommandHandler : IRequestHandler<MfaLoginCommand, LoginResult>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ITotpService _totp;

    public MfaLoginCommandHandler(
        IUserRepository users, IPasswordHasher hasher,
        IJwtTokenService jwt, IRefreshTokenRepository refreshTokens,
        ITotpService totp)
    {
        _users = users; _hasher = hasher; _jwt = jwt;
        _refreshTokens = refreshTokens; _totp = totp;
    }

    public async Task<LoginResult> Handle(MfaLoginCommand command, CancellationToken ct)
    {
        var user = await _users.FindByEmailAsync(new Email(command.Email), ct);

        if (user is null || !user.IsActive || user.IsLockedOut ||
            !_hasher.Verify(command.Password, user.PasswordHash))
        {
            if (user is not null && user.IsActive && !user.IsLockedOut)
            {
                user.RecordFailedLogin(LoginCommandHandler.MaxFailedAttempts, LoginCommandHandler.LockoutDuration);
                await _users.SaveAsync(user, ct);
            }
            throw new InvalidCredentialsException();
        }

        if (!user.MfaEnabled || user.MfaSecret is null)
            throw new InvalidCredentialsException();

        if (!_totp.Verify(user.MfaSecret, command.Code))
        {
            user.RecordFailedLogin(LoginCommandHandler.MaxFailedAttempts, LoginCommandHandler.LockoutDuration);
            await _users.SaveAsync(user, ct);
            throw new InvalidCredentialsException();
        }

        user.ResetFailedLogin();
        await _users.SaveAsync(user, ct);

        var rawToken = LoginCommandHandler.GenerateRawToken();
        var hash = LoginCommandHandler.HashToken(rawToken);
        var refreshToken = RefreshToken.Create(user.Id, hash, expiryDays: 7);
        await _refreshTokens.AddAsync(refreshToken, ct);

        return new LoginResult(_jwt.Generate(user), rawToken, user.Role.ToString());
    }
}
