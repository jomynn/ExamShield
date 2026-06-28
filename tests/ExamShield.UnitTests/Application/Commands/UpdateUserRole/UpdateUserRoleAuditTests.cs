using ExamShield.Application.Commands.UpdateUserRole;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.UpdateUserRole;

public sealed class UpdateUserRoleAuditTests
{
    private readonly IUserRepository          _users         = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository  _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IAuditLogRepository      _auditLog      = Substitute.For<IAuditLogRepository>();
    private readonly UpdateUserRoleCommandHandler _sut;

    public UpdateUserRoleAuditTests() =>
        _sut = new UpdateUserRoleCommandHandler(_users, _refreshTokens, _auditLog);

    [Fact]
    public async Task Handle_RoleChange_AppendsAuditEntry()
    {
        var userId = Guid.NewGuid();
        var user = User.Create(new Email("op@test.com"), "hash", UserRole.Operator);
        _users.GetByIdAsync(Arg.Any<UserId>(), default).Returns(user);

        await _sut.Handle(new UpdateUserRoleCommand(userId, "Supervisor"), default);

        await _auditLog.Received(1).AppendAsync(
            Arg.Is<AuditLog>(a => a.Action == AuditAction.UserRoleChanged), default);
    }

    [Fact]
    public async Task Handle_RoleChange_AuditRecordedAfterSave()
    {
        var userId = Guid.NewGuid();
        var user = User.Create(new Email("op@test.com"), "hash", UserRole.Operator);
        _users.GetByIdAsync(Arg.Any<UserId>(), default).Returns(user);

        await _sut.Handle(new UpdateUserRoleCommand(userId, "Supervisor"), default);

        Received.InOrder(() =>
        {
            _users.SaveAsync(user, default);
            _auditLog.AppendAsync(Arg.Any<AuditLog>(), default);
        });
    }
}
