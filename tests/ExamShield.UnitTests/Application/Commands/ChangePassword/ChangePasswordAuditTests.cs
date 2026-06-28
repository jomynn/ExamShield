using ExamShield.Application.Commands.ChangePassword;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.ChangePassword;

public sealed class ChangePasswordAuditTests
{
    private readonly IUserRepository         _users         = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher         _hasher        = Substitute.For<IPasswordHasher>();
    private readonly IRefreshTokenRepository _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IAuditLogRepository     _auditLog      = Substitute.For<IAuditLogRepository>();
    private readonly ChangePasswordCommandHandler _sut;

    public ChangePasswordAuditTests() =>
        _sut = new ChangePasswordCommandHandler(_users, _hasher, _refreshTokens, _auditLog);

    [Fact]
    public async Task Handle_PasswordChange_AppendsPasswordChangedAuditEntry()
    {
        var user = User.Create(new Email("u@test.com"), "hash", UserRole.Operator);
        _users.GetByIdAsync(Arg.Any<UserId>(), default).Returns(user);
        _hasher.Verify("OldPass1!", "hash").Returns(true);
        _hasher.Hash(Arg.Any<string>()).Returns("newhash");

        await _sut.Handle(
            new ChangePasswordCommand(Guid.NewGuid(), "OldPass1!", "NewPass1!"), default);

        await _auditLog.Received(1).AppendAsync(
            Arg.Is<AuditLog>(a => a.Action == AuditAction.PasswordChanged), default);
    }

    [Fact]
    public async Task Handle_PasswordChange_AuditAfterSave()
    {
        var user = User.Create(new Email("u@test.com"), "hash", UserRole.Operator);
        _users.GetByIdAsync(Arg.Any<UserId>(), default).Returns(user);
        _hasher.Verify("OldPass1!", "hash").Returns(true);
        _hasher.Hash(Arg.Any<string>()).Returns("newhash");

        await _sut.Handle(
            new ChangePasswordCommand(Guid.NewGuid(), "OldPass1!", "NewPass1!"), default);

        Received.InOrder(() =>
        {
            _users.SaveAsync(user, default);
            _auditLog.AppendAsync(Arg.Any<AuditLog>(), default);
        });
    }
}
