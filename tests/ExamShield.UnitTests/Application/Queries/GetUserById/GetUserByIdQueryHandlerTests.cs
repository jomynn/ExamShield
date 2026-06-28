using ExamShield.Application.Queries.GetUserById;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetUserById;

public sealed class GetUserByIdQueryHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly GetUserByIdQueryHandler _sut;

    public GetUserByIdQueryHandlerTests() =>
        _sut = new GetUserByIdQueryHandler(_users);

    [Fact]
    public async Task Handle_ExistingUser_ReturnsMappedDto()
    {
        var user = User.Create(new Email("alice@exam.io"), "hash", UserRole.Auditor);
        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        var result = await _sut.Handle(new GetUserByIdQuery(user.Id.Value), default);

        result.Should().NotBeNull();
        result!.UserId.Should().Be(user.Id.Value);
        result.Email.Should().Be("alice@exam.io");
        result.Role.Should().Be("Auditor");
        result.MfaEnabled.Should().BeFalse();
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_MissingUser_ReturnsNull()
    {
        _users.GetByIdAsync(Arg.Any<UserId>(), Arg.Any<CancellationToken>())
              .Returns((User?)null);

        var result = await _sut.Handle(new GetUserByIdQuery(Guid.NewGuid()), default);

        result.Should().BeNull();
    }
}
