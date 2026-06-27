using ExamShield.Application.Commands.MfaLogin;
using ExamShield.Application.Interfaces;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.MfaLogin;

public sealed class MfaLoginCommandHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IJwtTokenService _jwt = Substitute.For<IJwtTokenService>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly ITotpService _totp = Substitute.For<ITotpService>();
    private readonly MfaLoginCommandHandler _sut;

    private const string Secret = "JBSWY3DPEHPK3PXP";
    private const string ValidCode = "123456";

    public MfaLoginCommandHandlerTests() =>
        _sut = new MfaLoginCommandHandler(_users, _hasher, _jwt, _refreshTokens, _totp);

    private static User MakeMfaUser()
    {
        var user = User.Create(new Email("admin@examshield.io"), "$2a$04$hash", UserRole.Administrator);
        user.SetMfaSecret(Secret);
        user.EnableMfa();
        return user;
    }

    [Fact]
    public async Task Handle_WithValidCredentialsAndCode_ReturnsTokens()
    {
        var user = MakeMfaUser();
        _users.FindByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("password", user.PasswordHash).Returns(true);
        _totp.Verify(Secret, ValidCode).Returns(true);
        _jwt.Generate(user).Returns("jwt");

        var result = await _sut.Handle(
            new MfaLoginCommand("admin@examshield.io", "password", ValidCode), default);

        result.Token.Should().Be("jwt");
        result.RequiresMfa.Should().BeFalse();
        result.RefreshToken.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_WithInvalidCode_ThrowsUnauthorized()
    {
        var user = MakeMfaUser();
        _users.FindByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("password", user.PasswordHash).Returns(true);
        _totp.Verify(Secret, "000000").Returns(false);

        var act = () => _sut.Handle(
            new MfaLoginCommand("admin@examshield.io", "password", "000000"), default);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Handle_WithInvalidPassword_ThrowsInvalidCredentials()
    {
        var user = MakeMfaUser();
        _users.FindByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("wrong", user.PasswordHash).Returns(false);

        var act = () => _sut.Handle(
            new MfaLoginCommand("admin@examshield.io", "wrong", ValidCode), default);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Handle_WhenMfaNotEnabled_ThrowsInvalidCredentials()
    {
        var user = User.Create(new Email("op@examshield.io"), "$2a$04$hash", UserRole.Operator);
        _users.FindByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("password", user.PasswordHash).Returns(true);

        var act = () => _sut.Handle(
            new MfaLoginCommand("op@examshield.io", "password", ValidCode), default);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }
}
