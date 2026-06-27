using ExamShield.Application.Commands.ResetPassword;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.ResetPassword;

public sealed class ResetPasswordComplexityTests
{
    private readonly IPasswordResetTokenRepository _tokens        = Substitute.For<IPasswordResetTokenRepository>();
    private readonly IUserRepository               _users         = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher               _hasher        = Substitute.For<IPasswordHasher>();
    private readonly IRefreshTokenRepository       _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly ResetPasswordCommandHandler   _sut;

    public ResetPasswordComplexityTests() =>
        _sut = new ResetPasswordCommandHandler(_tokens, _users, _hasher, _refreshTokens);

    private void SetupValidToken(string email = "user@test.com")
    {
        var token = PasswordResetToken.Create(email);
        _tokens.FindAsync(Arg.Any<string>(), default).Returns(token);

        var user = User.Create(
            new Email(email), "OldHash1!", UserRole.Operator);
        _users.FindByEmailAsync(Arg.Any<Email>(), default).Returns(user);
        _hasher.Hash(Arg.Any<string>()).Returns("hashed");
    }

    [Theory]
    [InlineData("short")]
    [InlineData("alllowercase1!")]
    [InlineData("ALLUPPERCASE1!")]
    [InlineData("NoSpecialChar1")]
    [InlineData("NoDigit!Abc")]
    public async Task Handle_WeakPassword_ThrowsArgumentException(string weakPassword)
    {
        SetupValidToken();

        var act = () => _sut.Handle(new ResetPasswordCommand("valid-token", weakPassword), default);

        await act.Should().ThrowAsync<ArgumentException>();
        await _users.DidNotReceive().SaveAsync(Arg.Any<User>(), default);
    }

    [Fact]
    public async Task Handle_StrongPassword_Succeeds()
    {
        SetupValidToken();

        var act = () => _sut.Handle(new ResetPasswordCommand("valid-token", "StrongPass1!"), default);

        await act.Should().NotThrowAsync();
        await _users.Received(1).SaveAsync(Arg.Any<User>(), default);
    }
}
