using ExamShield.Application.Queries.ExportUsers;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.ExportUsers;

public sealed class ExportUsersQueryHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly ExportUsersQueryHandler _sut;

    public ExportUsersQueryHandlerTests() => _sut = new(_users);

    private static User MakeUser(string email, UserRole role = UserRole.Invigilator) =>
        User.Create(new Email(email), "hash", role);

    [Fact]
    public async Task Handle_NoFilter_ExportsAllUsers()
    {
        IReadOnlyList<User> list = [MakeUser("a@b.io"), MakeUser("c@d.io")];
        _users.ListAllAsync(default).Returns(list);

        var result = await _sut.Handle(new(), default);

        result.Csv.Trim().Split('\n').Should().HaveCount(3); // header + 2
    }

    [Fact]
    public async Task Handle_SearchFilter_OnlyMatchingEmails()
    {
        IReadOnlyList<User> list = [MakeUser("alice@exam.io"), MakeUser("bob@exam.io")];
        _users.ListAllAsync(default).Returns(list);

        var result = await _sut.Handle(new(Search: "alice"), default);

        result.Csv.Should().Contain("alice@exam.io");
        result.Csv.Should().NotContain("bob@exam.io");
    }

    [Fact]
    public async Task Handle_RoleFilter_OnlyMatchingRole()
    {
        IReadOnlyList<User> list = [MakeUser("inv@a.io", UserRole.Invigilator), MakeUser("rev@a.io", UserRole.ManualReviewer)];
        _users.ListAllAsync(default).Returns(list);

        var result = await _sut.Handle(new(Role: "ManualReviewer"), default);

        result.Csv.Should().Contain("rev@a.io");
        result.Csv.Should().NotContain("inv@a.io");
    }

    [Fact]
    public async Task Handle_IsActiveFilter_OnlyActiveUsers()
    {
        var active = MakeUser("a@a.io");
        var inactive = MakeUser("b@a.io");
        inactive.Deactivate();
        IReadOnlyList<User> list = [active, inactive];
        _users.ListAllAsync(default).Returns(list);

        var result = await _sut.Handle(new(IsActive: true), default);

        result.Csv.Should().Contain("a@a.io");
        result.Csv.Should().NotContain("b@a.io");
    }

    [Fact]
    public async Task Handle_CsvHasExpectedHeader()
    {
        IReadOnlyList<User> empty = [];
        _users.ListAllAsync(default).Returns(empty);

        var result = await _sut.Handle(new(), default);

        result.Csv.Should().Contain("UserId,Email,Role,IsActive,CreatedAt");
        result.Filename.Should().StartWith("users-");
    }
}
