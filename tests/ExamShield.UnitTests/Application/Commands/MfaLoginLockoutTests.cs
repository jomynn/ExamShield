using ExamShield.Application.Commands.Login;
using ExamShield.Application.Commands.MfaLogin;
using ExamShield.Application.Interfaces;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands;

public sealed class MfaLoginLockoutTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenService _jwt = Substitute.For<IJwtTokenService>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly ITotpService _totp = Substitute.For<ITotpService>();
    private readonly MfaLoginCommandHandler _sut;

    public MfaLoginLockoutTests() =>
        _sut = new MfaLoginCommandHandler(_users, _hasher, _jwt, _refreshTokens, _totp);

    private static User MakeMfaUser()
    {
        var user = User.Create(new Email("mfa@test.com"), "hash", UserRole.Operator);
        user.SetMfaSecret("SECRET");
        user.EnableMfa();
        return user;
    }

    [Fact]
    public async Task Handle_WhenUserIsLockedOut_ThrowsInvalidCredentialsException()
    {
        var user = MakeMfaUser();
        for (var i = 0; i < LoginCommandHandler.MaxFailedAttempts; i++)
            user.RecordFailedLogin(LoginCommandHandler.MaxFailedAttempts, LoginCommandHandler.LockoutDuration);

        _users.FindByEmailAsync(Arg.Any<Email>(), default).Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _sut.Handle(new MfaLoginCommand("mfa@test.com", "pass", "123456"), default));
    }

    [Fact]
    public async Task Handle_WhenMfaCodeInvalid_IncrementsFailedAttempts()
    {
        var user = MakeMfaUser();
        _users.FindByEmailAsync(Arg.Any<Email>(), default).Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _totp.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        for (var i = 0; i < LoginCommandHandler.MaxFailedAttempts; i++)
        {
            try { await _sut.Handle(new MfaLoginCommand("mfa@test.com", "pass", "bad"), default); }
            catch { /* expected */ }
        }

        Assert.True(user.IsLockedOut);
    }

    [Fact]
    public async Task Handle_WhenMfaCodeValid_ResetsFailedAttempts()
    {
        var user = MakeMfaUser();
        user.RecordFailedLogin(LoginCommandHandler.MaxFailedAttempts, LoginCommandHandler.LockoutDuration);
        user.ResetFailedLogin();
        user.RecordFailedLogin(LoginCommandHandler.MaxFailedAttempts, LoginCommandHandler.LockoutDuration);
        user.ResetFailedLogin();

        _users.FindByEmailAsync(Arg.Any<Email>(), default).Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _totp.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _jwt.Generate(Arg.Any<User>()).Returns("tok");

        await _sut.Handle(new MfaLoginCommand("mfa@test.com", "pass", "123456"), default);

        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.Null(user.LockedUntil);
    }
}
