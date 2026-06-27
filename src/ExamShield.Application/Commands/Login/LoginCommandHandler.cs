using System.Security.Cryptography;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.Login;

public sealed class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IJwtTokenService _jwt;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ISecurityEventRepository _security;

    public LoginCommandHandler(
        IUserRepository users, IPasswordHasher hasher,
        IJwtTokenService jwt, IRefreshTokenRepository refreshTokens,
        ISecurityEventRepository security)
    {
        _users = users; _hasher = hasher; _jwt = jwt;
        _refreshTokens = refreshTokens; _security = security;
    }

    public async Task<LoginResult> Handle(LoginCommand command, CancellationToken ct)
    {
        var user = await _users.FindByEmailAsync(new Email(command.Email), ct);

        if (user is null || !user.IsActive || user.IsLockedOut ||
            !_hasher.Verify(command.Password, user.PasswordHash))
        {
            if (user is not null && user.IsActive && !user.IsLockedOut)
            {
                user.RecordFailedLogin(MaxFailedAttempts, LockoutDuration);
                await _users.SaveAsync(user, ct);
            }

            await _security.AddAsync(SecurityEvent.Create(
                SecurityEventType.LoginFailed, SecuritySeverity.Warning,
                $"Failed login attempt for {command.Email}",
                ipAddress: command.IpAddress), ct);
            throw new InvalidCredentialsException();
        }

        user.ResetFailedLogin();
        await _users.SaveAsync(user, ct);

        if (user.MfaEnabled)
            return new LoginResult(string.Empty, string.Empty, user.Role.ToString(), RequiresMfa: true);

        var rawToken = GenerateRawToken();
        var hash = HashToken(rawToken);
        var refreshToken = RefreshToken.Create(user.Id, hash, expiryDays: 7);
        await _refreshTokens.AddAsync(refreshToken, ct);

        await _security.AddAsync(SecurityEvent.Create(
            SecurityEventType.LoginSuccess, SecuritySeverity.Info,
            $"Successful login for {command.Email}",
            userId: user.Id.Value.ToString(),
            ipAddress: command.IpAddress), ct);

        return new LoginResult(_jwt.Generate(user), rawToken, user.Role.ToString());
    }

    internal static string GenerateRawToken() =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    internal static string HashToken(string raw) =>
        Convert.ToBase64String(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(raw)));
}
