using ExamShield.Application.Interfaces;
using ExamShield.Application.Queries.GetSetupStatus;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace ExamShield.UnitTests.Application.Queries;

public sealed class GetSetupStatusQueryHandlerTests
{
    private readonly IUserRepository       _users  = Substitute.For<IUserRepository>();
    private readonly ISystemHealthService  _health = Substitute.For<ISystemHealthService>();
    private readonly GetSetupStatusQueryHandler _sut;

    private static readonly IReadOnlyDictionary<string, string> HealthyChecks =
        new Dictionary<string, string> { ["postgres"] = "Healthy", ["redis"] = "Healthy" };

    public GetSetupStatusQueryHandlerTests()
    {
        _sut = new GetSetupStatusQueryHandler(_users, _health);
        _health.CheckAsync(Arg.Any<CancellationToken>()).Returns(HealthyChecks);
    }

    [Fact]
    public async Task Handle_NoSuperAdmin_IsFirstRunTrue()
    {
        _users.ListAllAsync(Arg.Any<CancellationToken>()).Returns(new List<User>());

        var result = await _sut.Handle(new GetSetupStatusQuery(), default);

        result.IsFirstRun.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_SuperAdminExists_IsFirstRunFalse()
    {
        var admin = User.Create(
            new Email("super@examshield.local"),
            "hash",
            UserRole.SuperAdministrator);
        _users.ListAllAsync(Arg.Any<CancellationToken>()).Returns(new List<User> { admin });

        var result = await _sut.Handle(new GetSetupStatusQuery(), default);

        result.IsFirstRun.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_AdminButNotSuperAdmin_IsFirstRunTrue()
    {
        var admin = User.Create(new Email("admin@examshield.local"), "hash", UserRole.Administrator);
        _users.ListAllAsync(Arg.Any<CancellationToken>()).Returns(new List<User> { admin });

        var result = await _sut.Handle(new GetSetupStatusQuery(), default);

        result.IsFirstRun.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ReturnsVersionString()
    {
        _users.ListAllAsync(Arg.Any<CancellationToken>()).Returns(new List<User>());

        var result = await _sut.Handle(new GetSetupStatusQuery(), default);

        result.Version.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Handle_ReturnsHealthChecks()
    {
        _users.ListAllAsync(Arg.Any<CancellationToken>()).Returns(new List<User>());

        var result = await _sut.Handle(new GetSetupStatusQuery(), default);

        result.Checks.Should().ContainKey("postgres");
        result.Checks["postgres"].Should().Be("Healthy");
    }
}
