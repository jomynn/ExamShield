using ExamShield.Application.Commands.ResetPassword;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.ResetPassword;

public sealed class ResetPasswordAuditTests
{
    private readonly IPasswordResetTokenRepository _tokens        = Substitute.For<IPasswordResetTokenRepository>();
    private readonly IUserRepository               _users         = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher               _hasher        = Substitute.For<IPasswordHasher>();
    private readonly IRefreshTokenRepository       _refreshTokens = Substitute.For<IRefreshTokenRepository>();
    private readonly IAuditLogRepository           _auditLog      = Substitute.For<IAuditLogRepository>();
    private readonly ResetPasswordCommandHandler   _sut;

    public ResetPasswordAuditTests() =>
        _sut = new ResetPasswordCommandHandler(_tokens, _users, _hasher, _refreshTokens, _auditLog);

    [Fact]
    public async Task Handle_PasswordReset_AppendsPasswordResetAuditEntry()
    {
        const string email = "u@test.com";
        var token = PasswordResetToken.Create(email);
        _tokens.FindAsync(Arg.Any<string>(), default).Returns(token);
        var user = User.Create(new Email(email), "hash", UserRole.Operator);
        _users.FindByEmailAsync(Arg.Any<Email>(), default).Returns(user);
        _hasher.Hash(Arg.Any<string>()).Returns("newhash");

        await _sut.Handle(new ResetPasswordCommand("valid-token", "NewPass1!"), default);

        await _auditLog.Received(1).AppendAsync(
            Arg.Is<AuditLog>(a => a.Action == AuditAction.PasswordReset), default);
    }
}
