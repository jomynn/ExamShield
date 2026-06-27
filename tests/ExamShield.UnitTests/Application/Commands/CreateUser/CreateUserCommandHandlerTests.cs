using ExamShield.Application.Commands.CreateUser;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.CreateUser;

public sealed class CreateUserCommandHandlerTests
{
    private const string ValidPassword = "Str0ng!Password";

    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IAuditLogRepository _auditLog = Substitute.For<IAuditLogRepository>();
    private readonly CreateUserCommandHandler _sut;

    public CreateUserCommandHandlerTests()
    {
        _hasher.Hash(Arg.Any<string>()).Returns("$2a$04$hashed");
        _sut = new CreateUserCommandHandler(_users, _hasher, _auditLog);
    }

    [Fact]
    public async Task Handle_WithValidData_ReturnsNewUserId()
    {
        _users.FindByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        var result = await _sut.Handle(
            new CreateUserCommand("new@test.com", ValidPassword, UserRole.Operator), default);

        result.UserId.Should().NotBe(Guid.Empty);
        await _users.Received(1).AddAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenEmailAlreadyExists_ThrowsUserAlreadyExistsException()
    {
        var existing = User.Create(new Email("taken@test.com"), "$2a$04$h", UserRole.Operator);
        _users.FindByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(existing);

        var act = () => _sut.Handle(
            new CreateUserCommand("taken@test.com", ValidPassword, UserRole.Operator), default);

        await act.Should().ThrowAsync<UserAlreadyExistsException>();
    }

    [Fact]
    public async Task Handle_HashesPasswordBeforeStoring()
    {
        _users.FindByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>())
            .Returns((User?)null);

        await _sut.Handle(
            new CreateUserCommand("u@test.com", ValidPassword, UserRole.Auditor), default);

        _hasher.Received(1).Hash(ValidPassword);
    }
}
