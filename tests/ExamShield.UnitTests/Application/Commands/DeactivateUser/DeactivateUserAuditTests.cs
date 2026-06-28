using ExamShield.Application.Commands.DeactivateUser;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.DeactivateUser;

public sealed class DeactivateUserAuditTests
{
    private readonly IUserRepository         _users         = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _tokens        = Substitute.For<IRefreshTokenRepository>();
    private readonly IAuditLogRepository     _auditLog      = Substitute.For<IAuditLogRepository>();
    private readonly DeactivateUserCommandHandler _sut;

    public DeactivateUserAuditTests() =>
        _sut = new DeactivateUserCommandHandler(_users, _tokens, _auditLog);

    [Fact]
    public async Task Handle_Deactivation_AppendsAuditEntry()
    {
        var user = User.Create(new Email("op@test.com"), "hash", UserRole.Operator);
        _users.GetByIdAsync(Arg.Any<UserId>(), default).Returns(user);

        await _sut.Handle(new DeactivateUserCommand(Guid.NewGuid()), default);

        await _auditLog.Received(1).AppendAsync(
            Arg.Is<AuditLog>(a => a.Action == AuditAction.UserDeactivated), default);
    }

    [Fact]
    public async Task Handle_Deactivation_AuditAfterSave()
    {
        var user = User.Create(new Email("op@test.com"), "hash", UserRole.Operator);
        _users.GetByIdAsync(Arg.Any<UserId>(), default).Returns(user);

        await _sut.Handle(new DeactivateUserCommand(Guid.NewGuid()), default);

        Received.InOrder(() =>
        {
            _users.SaveAsync(user, default);
            _auditLog.AppendAsync(Arg.Any<AuditLog>(), default);
        });
    }
}
