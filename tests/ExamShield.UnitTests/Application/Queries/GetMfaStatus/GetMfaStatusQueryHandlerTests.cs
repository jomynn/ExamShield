using ExamShield.Application.Queries.GetMfaStatus;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetMfaStatus;

public sealed class GetMfaStatusQueryHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly GetMfaStatusQueryHandler _sut;

    public GetMfaStatusQueryHandlerTests() =>
        _sut = new GetMfaStatusQueryHandler(_users);

    [Fact]
    public async Task Handle_UserWithMfaDisabled_ReturnsFalse()
    {
        var user = User.Create(new Email("u@exam.io"), "hash", UserRole.Invigilator);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.Handle(new GetMfaStatusQuery(user.Id.Value), default);

        result.MfaEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_UserWithMfaEnabled_ReturnsTrue()
    {
        var user = User.Create(new Email("u@exam.io"), "hash", UserRole.Administrator);
        user.SetMfaSecret("JBSWY3DPEHPK3PXP");
        user.EnableMfa();
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.Handle(new GetMfaStatusQuery(user.Id.Value), default);

        result.MfaEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MissingUser_ThrowsInvalidOperationException()
    {
        _users.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
              .Returns((User?)null);

        var act = () => _sut.Handle(new GetMfaStatusQuery(Guid.NewGuid()), default);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
