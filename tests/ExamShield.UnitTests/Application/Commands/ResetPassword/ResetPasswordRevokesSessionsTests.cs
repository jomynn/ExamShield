using ExamShield.Application.Commands.ResetPassword;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.ResetPassword;

public sealed class ResetPasswordRevokesSessionsTests
{
    private readonly IPasswordResetTokenRepository _tokens        = Substitute.For<IPasswordResetTokenRepository>();
    private readonly IUserRepository               _users         = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher               _hasher        = Substitute.For<IPasswordHasher>();
    private readonly IRefreshTokenRepository       _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly ResetPasswordCommandHandler   _sut;

    public ResetPasswordRevokesSessionsTests() =>
        _sut = new ResetPasswordCommandHandler(_tokens, _users, _hasher, _refreshTokens);

    [Fact]
    public async Task Handle_ValidReset_RevokesAllRefreshTokens()
    {
        const string email = "victim@test.com";
        var token = PasswordResetToken.Create(email);
        _tokens.FindAsync(Arg.Any<string>(), default).Returns(token);

        var user = User.Create(new Email(email), "OldHash1!", UserRole.Operator);
        _users.FindByEmailAsync(Arg.Any<Email>(), default).Returns(user);
        _hasher.Hash(Arg.Any<string>()).Returns("NewHashedPass1!");

        await _sut.Handle(new ResetPasswordCommand("valid-token", "NewPass1!"), default);

        await _refreshTokens.Received(1).RevokeAllForUserAsync(user.Id, default);
    }

    [Fact]
    public async Task Handle_ValidReset_SavesUserBeforeRevokingSessions()
    {
        const string email = "victim@test.com";
        var token = PasswordResetToken.Create(email);
        _tokens.FindAsync(Arg.Any<string>(), default).Returns(token);

        var user = User.Create(new Email(email), "OldHash1!", UserRole.Operator);
        _users.FindByEmailAsync(Arg.Any<Email>(), default).Returns(user);
        _hasher.Hash(Arg.Any<string>()).Returns("NewHashedPass1!");

        await _sut.Handle(new ResetPasswordCommand("valid-token", "NewPass1!"), default);

        Received.InOrder(() =>
        {
            _users.SaveAsync(user, default);
            _refreshTokens.RevokeAllForUserAsync(user.Id, default);
        });
    }
}
