using ExamShield.Application.Queries.GetUsers;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetUsers;

public sealed class GetUsersQueryHandlerTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly GetUsersQueryHandler _sut;

    public GetUsersQueryHandlerTests() => _sut = new(_users);

    private static User MakeUser(string email = "a@b.io") =>
        User.Create(new Email(email), "hash", UserRole.Invigilator);

    private static GetUsersQuery PageQuery(int page = 1, int size = 10) =>
        new(page, size, null, null, null);

    [Fact]
    public async Task Handle_ReturnsPagedUserDtos()
    {
        IReadOnlyList<User> list = [MakeUser("alice@b.io"), MakeUser("bob@b.io")];
        _users.ListPagedAsync(1, 10, null, null, null, default).Returns((list, 2));

        var result = await _sut.Handle(PageQuery(), default);

        result.Users.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_MapsEmailAndRole()
    {
        IReadOnlyList<User> list = [MakeUser("z@b.io")];
        _users.ListPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<bool?>(), default).Returns((list, 1));

        var result = await _sut.Handle(PageQuery(), default);

        result.Users[0].Email.Should().Be("z@b.io");
        result.Users[0].Role.Should().Be("Invigilator");
    }

    [Fact]
    public async Task Handle_SortsAlphabeticallyByEmail()
    {
        IReadOnlyList<User> list = [MakeUser("z@b.io"), MakeUser("a@b.io")];
        _users.ListPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<bool?>(), default).Returns((list, 2));

        var result = await _sut.Handle(PageQuery(), default);

        result.Users[0].Email.Should().Be("a@b.io");
        result.Users[1].Email.Should().Be("z@b.io");
    }

    [Fact]
    public async Task Handle_EmptyList_ReturnsEmptyWithZeroTotal()
    {
        IReadOnlyList<User> empty = [];
        _users.ListPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(),
            Arg.Any<string?>(), Arg.Any<bool?>(), default).Returns((empty, 0));

        var result = await _sut.Handle(PageQuery(), default);

        result.Users.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }
}
