using ExamShield.Application.Commands.ChangePassword;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.ChangePassword;

public sealed class ChangePasswordCommandHandlerTests
{
    private const string NewPassword = "N3wStr0ng!Pass";

    private readonly IUserRepository         _users         = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher         _hasher        = Substitute.For<IPasswordHasher>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IAuditLogRepository     _auditLog      = Substitute.For<IAuditLogRepository>();
    private readonly ChangePasswordCommandHandler _sut;

    private static readonly Guid _userId = Guid.NewGuid();

    public ChangePasswordCommandHandlerTests() =>
        _sut = new ChangePasswordCommandHandler(_users, _hasher, _refreshTokens, _auditLog);

    private static User MakeUser() =>
        User.Create(new Email("user@examshield.io"), "old-hash", UserRole.Operator);

    [Fact]
    public async Task Handle_WithCorrectCurrentPassword_UpdatesPasswordHash()
    {
        var user = MakeUser();
        _users.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("old-pw", "old-hash").Returns(true);
        _hasher.Hash(NewPassword).Returns("new-hash");

        await _sut.Handle(new ChangePasswordCommand(_userId, "old-pw", NewPassword), default);

        user.PasswordHash.Should().Be("new-hash");
        await _users.Received(1).SaveAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithCorrectCurrentPassword_RevokesAllRefreshTokens()
    {
        var user = MakeUser();
        _users.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("old-pw", "old-hash").Returns(true);
        _hasher.Hash(NewPassword).Returns("new-hash");

        await _sut.Handle(new ChangePasswordCommand(_userId, "old-pw", NewPassword), default);

        await _refreshTokens.Received(1).RevokeAllForUserAsync(user.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithWrongCurrentPassword_ThrowsInvalidCredentials()
    {
        var user = MakeUser();
        _users.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("wrong", "old-hash").Returns(false);

        var act = () => _sut.Handle(new ChangePasswordCommand(_userId, "wrong", NewPassword), default);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ThrowsInvalidCredentials()
    {
        _users.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns((User?)null);

        var act = () => _sut.Handle(new ChangePasswordCommand(_userId, "any", NewPassword), default);

        await act.Should().ThrowAsync<InvalidCredentialsException>();
    }

    [Fact]
    public async Task Handle_WithWrongPassword_DoesNotSaveUser()
    {
        var user = MakeUser();
        _users.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>()).Returns(user);
        _hasher.Verify("wrong", "old-hash").Returns(false);

        try { await _sut.Handle(new ChangePasswordCommand(_userId, "wrong", NewPassword), default); }
        catch { /* expected */ }

        await _users.DidNotReceive().SaveAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }
}
