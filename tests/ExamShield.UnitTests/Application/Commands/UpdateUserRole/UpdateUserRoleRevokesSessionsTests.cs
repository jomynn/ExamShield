using ExamShield.Application.Commands.UpdateUserRole;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.UpdateUserRole;

public sealed class UpdateUserRoleRevokesSessionsTests
{
    private readonly IUserRepository          _users         = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository  _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IAuditLogRepository      _auditLog      = Substitute.For<IAuditLogRepository>();
    private readonly UpdateUserRoleCommandHandler _sut;

    public UpdateUserRoleRevokesSessionsTests() =>
        _sut = new UpdateUserRoleCommandHandler(_users, _refreshTokens, _auditLog);

    [Fact]
    public async Task Handle_WhenRoleChanges_RevokesAllRefreshTokens()
    {
        var userId = new UserId(Guid.NewGuid());
        var user   = User.Create(new Email("operator@test.com"), "hashed", UserRole.Operator);
        _users.GetByIdAsync(userId, default).Returns(user);

        await _sut.Handle(new UpdateUserRoleCommand(userId.Value, "Student"), default);

        await _refreshTokens.Received(1).RevokeAllForUserAsync(user.Id, default);
    }

    [Fact]
    public async Task Handle_WhenRoleChanges_SavesUpdatedUser()
    {
        var userId = new UserId(Guid.NewGuid());
        var user   = User.Create(new Email("op@test.com"), "hashed", UserRole.Operator);
        _users.GetByIdAsync(userId, default).Returns(user);

        await _sut.Handle(new UpdateUserRoleCommand(userId.Value, "Auditor"), default);

        await _users.Received(1).SaveAsync(
            Arg.Is<User>(u => u.Role == UserRole.Auditor), default);
    }
}
