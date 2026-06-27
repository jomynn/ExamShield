using ExamShield.Application.Commands.Login;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands;

public sealed class LoginLockoutTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenService _jwt = Substitute.For<IJwtTokenService>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly ISecurityEventRepository _security = Substitute.For<ISecurityEventRepository>();
    private readonly LoginCommandHandler _sut;

    public LoginLockoutTests() =>
        _sut = new LoginCommandHandler(_users, _hasher, _jwt, _refreshTokens, _security);

    private static User MakeUser(string password = "correct")
    {
        var user = User.Create(new Email("user@test.com"), "hash", UserRole.Operator);
        return user;
    }

    [Fact]
    public async Task Handle_AfterMaxFailedAttempts_LocksAccount()
    {
        var user = MakeUser();
        _users.FindByEmailAsync(Arg.Any<Email>(), default).Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(false);

        for (var i = 0; i < LoginCommandHandler.MaxFailedAttempts; i++)
        {
            await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
                _sut.Handle(new LoginCommand("user@test.com", "wrong", null), default));
        }

        Assert.True(user.IsLockedOut);
    }

    [Fact]
    public async Task Handle_WhenLockedOut_RejectsCorrectPassword()
    {
        var user = MakeUser();
        for (var i = 0; i < LoginCommandHandler.MaxFailedAttempts; i++)
            user.RecordFailedLogin(LoginCommandHandler.MaxFailedAttempts, LoginCommandHandler.LockoutDuration);

        _users.FindByEmailAsync(Arg.Any<Email>(), default).Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);

        await Assert.ThrowsAsync<InvalidCredentialsException>(() =>
            _sut.Handle(new LoginCommand("user@test.com", "correct", null), default));
    }

    [Fact]
    public async Task Handle_SuccessfulLogin_ResetsFailedAttempts()
    {
        var user = MakeUser();
        user.RecordFailedLogin(LoginCommandHandler.MaxFailedAttempts, LoginCommandHandler.LockoutDuration);
        user.ResetFailedLogin();
        user.RecordFailedLogin(LoginCommandHandler.MaxFailedAttempts, LoginCommandHandler.LockoutDuration);
        user.ResetFailedLogin();

        _users.FindByEmailAsync(Arg.Any<Email>(), default).Returns(user);
        _hasher.Verify(Arg.Any<string>(), Arg.Any<string>()).Returns(true);
        _jwt.Generate(Arg.Any<User>()).Returns("token");

        await _sut.Handle(new LoginCommand("user@test.com", "correct", null), default);

        Assert.Equal(0, user.FailedLoginAttempts);
        Assert.False(user.IsLockedOut);
    }
}
