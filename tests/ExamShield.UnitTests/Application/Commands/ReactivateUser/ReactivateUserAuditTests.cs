using ExamShield.Application.Commands.ReactivateUser;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.ReactivateUser;

public sealed class ReactivateUserAuditTests
{
    private readonly IUserRepository         _users    = Substitute.For<IUserRepository>();
    private readonly IAuditLogRepository     _auditLog = Substitute.For<IAuditLogRepository>();
    private readonly ReactivateUserCommandHandler _sut;

    public ReactivateUserAuditTests() =>
        _sut = new ReactivateUserCommandHandler(_users, _auditLog);

    [Fact]
    public async Task Handle_Reactivation_AppendsAuditEntry()
    {
        var user = User.Create(new Email("op@test.com"), "hash", UserRole.Operator);
        user.Deactivate();
        _users.GetByIdAsync(Arg.Any<UserId>(), default).Returns(user);

        await _sut.Handle(new ReactivateUserCommand(Guid.NewGuid()), default);

        await _auditLog.Received(1).AppendAsync(
            Arg.Is<AuditLog>(a => a.Action == AuditAction.UserReactivated), default);
    }
}
