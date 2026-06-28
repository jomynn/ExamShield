using ExamShield.Application.Commands.DeactivateUser;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands;

public sealed class DeactivateUserRevokesSessionsTests
{
    private readonly IUserRepository         _users    = Substitute.For<IUserRepository>();
    private readonly IRefreshTokenRepository _tokens   = Substitute.For<IRefreshTokenRepository>();
    private readonly IAuditLogRepository     _auditLog = Substitute.For<IAuditLogRepository>();

    private DeactivateUserCommandHandler BuildSut() => new(_users, _tokens, _auditLog);

    private static User MakeUser()
    {
        var user = User.Create(new Email("test@example.com"), "hash", UserRole.Operator);
        return user;
    }

    [Fact]
    public async Task Handle_DeactivatesUser_AndRevokesAllSessions()
    {
        var user = MakeUser();
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        await BuildSut().Handle(new DeactivateUserCommand(user.Id.Value), default);

        await _tokens.Received(1)
            .RevokeAllForUserAsync(user.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SavesUser_BeforeRevokingSessions()
    {
        var user = MakeUser();
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var callOrder = new List<string>();
        _users.SaveAsync(Arg.Any<User>(), Arg.Any<CancellationToken>())
            .Returns(_ => { callOrder.Add("save"); return Task.CompletedTask; });
        _tokens.RevokeAllForUserAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns(_ => { callOrder.Add("revoke"); return Task.CompletedTask; });

        await BuildSut().Handle(new DeactivateUserCommand(user.Id.Value), default);

        Assert.Equal(["save", "revoke"], callOrder);
    }

    [Fact]
    public async Task Handle_UserNotFound_Throws_AndDoesNotRevokeAnySessions()
    {
        _users.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        await Assert.ThrowsAsync<UserNotFoundException>(
            () => BuildSut().Handle(new DeactivateUserCommand(Guid.NewGuid()), default));

        await _tokens.DidNotReceive()
            .RevokeAllForUserAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_AlreadyInactiveUser_StillRevokesAnyStaleSessions()
    {
        // Deactivate() is idempotent at the domain level; sessions must still be revoked.
        var user = MakeUser();
        user.Deactivate();
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        await BuildSut().Handle(new DeactivateUserCommand(user.Id.Value), default);

        await _tokens.Received(1)
            .RevokeAllForUserAsync(user.Id, Arg.Any<CancellationToken>());
    }
}
