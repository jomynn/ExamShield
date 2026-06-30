using ExamShield.Application.Commands.CompleteSetup;
using ExamShield.Application.Interfaces;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace ExamShield.UnitTests.Application.Commands;

public sealed class CompleteSetupCommandHandlerTests
{
    private readonly IUserRepository       _users      = Substitute.For<IUserRepository>();
    private readonly IPasswordHasher       _hasher     = Substitute.For<IPasswordHasher>();
    private readonly IAuditLogRepository   _auditLog   = Substitute.For<IAuditLogRepository>();
    private readonly IServiceScopeFactory  _scopeFactory = Substitute.For<IServiceScopeFactory>();
    private readonly CompleteSetupCommandHandler _sut;

    private const string ValidEmail    = "admin@examshield.local";
    private const string ValidName     = "Super Admin";
    private const string ValidPassword = "Admin@123!";

    public CompleteSetupCommandHandlerTests()
    {
        _sut = new CompleteSetupCommandHandler(_users, _hasher, _auditLog, _scopeFactory);
        _hasher.Hash(Arg.Any<string>()).Returns("hashed-password");
        _users.ListAllAsync(Arg.Any<CancellationToken>()).Returns(new List<User>());
        _users.FindByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns((User?)null);
    }

    private CompleteSetupCommand DefaultCommand(bool seedDemo = false) =>
        new(ValidEmail, ValidName, ValidPassword, seedDemo);

    [Fact]
    public async Task Handle_FirstRun_CreatesAdminUser()
    {
        await _sut.Handle(DefaultCommand(), default);

        await _users.Received(1).AddAsync(
            Arg.Is<User>(u => u.Role == UserRole.SuperAdministrator),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_FirstRun_ReturnsNewUserId()
    {
        var result = await _sut.Handle(DefaultCommand(), default);

        result.AdminUserId.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Handle_FirstRun_AppendsAuditLog()
    {
        await _sut.Handle(DefaultCommand(), default);

        await _auditLog.Received(1).AppendAsync(
            Arg.Any<AuditLog>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SetupAlreadyCompleted_Throws()
    {
        var existing = User.Create(
            new Email("existing@examshield.local"), "hash", UserRole.SuperAdministrator);
        _users.ListAllAsync(Arg.Any<CancellationToken>()).Returns(new List<User> { existing });

        await _sut.Invoking(s => s.Handle(DefaultCommand(), default))
                  .Should().ThrowAsync<InvalidOperationException>()
                  .WithMessage("*already been completed*");
    }

    [Fact]
    public async Task Handle_EmailAlreadyExists_ThrowsUserAlreadyExists()
    {
        var existing = User.Create(new Email(ValidEmail), "hash", UserRole.Invigilator);
        _users.FindByEmailAsync(Arg.Any<Email>(), Arg.Any<CancellationToken>()).Returns(existing);

        await _sut.Invoking(s => s.Handle(DefaultCommand(), default))
                  .Should().ThrowAsync<UserAlreadyExistsException>();
    }

    [Fact]
    public async Task Handle_SeedDemoFalse_DoesNotCallSeeder()
    {
        await _sut.Handle(DefaultCommand(seedDemo: false), default);

        _scopeFactory.DidNotReceive().CreateScope();
    }

    [Fact]
    public async Task Handle_SeedDemoTrue_CallsSeeder()
    {
        var scope    = Substitute.For<IServiceScope>();
        var provider = Substitute.For<IServiceProvider>();
        var seeder   = Substitute.For<IDemoDataSeeder>();
        _scopeFactory.CreateScope().Returns(scope);
        scope.ServiceProvider.Returns(provider);
        provider.GetService(typeof(IDemoDataSeeder)).Returns(seeder);

        await _sut.Handle(DefaultCommand(seedDemo: true), default);

        await seeder.Received(1).SeedAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_HashesPassword_BeforeStoringUser()
    {
        await _sut.Handle(DefaultCommand(), default);

        _hasher.Received(1).Hash(ValidPassword);
        await _users.Received(1).AddAsync(
            Arg.Is<User>(u => u.PasswordHash == "hashed-password"),
            Arg.Any<CancellationToken>());
    }
}
