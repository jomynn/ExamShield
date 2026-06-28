using ExamShield.Application.Commands.MfaDisable;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.MfaDisable;

public sealed class MfaDisableAuditTests
{
    private readonly IUserRepository     _users    = Substitute.For<IUserRepository>();
    private readonly IAuditLogRepository _auditLog = Substitute.For<IAuditLogRepository>();
    private readonly MfaDisableCommandHandler _sut;

    public MfaDisableAuditTests() =>
        _sut = new MfaDisableCommandHandler(_users, _auditLog);

    [Fact]
    public async Task Handle_MfaDisable_AppendsMfaDisabledAuditEntry()
    {
        var user = User.Create(new Email("u@test.com"), "hash", UserRole.Operator);
        user.SetMfaSecret("SECRET");
        user.EnableMfa();
        _users.GetByIdAsync(Arg.Any<UserId>(), default).Returns(user);

        await _sut.Handle(new MfaDisableCommand(Guid.NewGuid()), default);

        await _auditLog.Received(1).AppendAsync(
            Arg.Is<AuditLog>(a => a.Action == AuditAction.MfaDisabled), default);
    }
}
