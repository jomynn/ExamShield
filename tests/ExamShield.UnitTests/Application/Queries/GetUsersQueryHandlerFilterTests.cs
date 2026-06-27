using ExamShield.Application.Queries.GetUsers;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.Entities;
using ExamShield.Domain.ValueObjects;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries;

public sealed class GetUsersQueryHandlerFilterTests
{
    private readonly IUserRepository _repo = Substitute.For<IUserRepository>();
    private readonly GetUsersQueryHandler _sut;

    public GetUsersQueryHandlerFilterTests() => _sut = new GetUsersQueryHandler(_repo);

    [Fact]
    public async Task Handle_SearchFilter_PassedToRepository()
    {
        _repo.ListPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool?>(), Arg.Any<CancellationToken>())
            .Returns((new List<User>(), 0));

        await _sut.Handle(new GetUsersQuery(Search: "alice"), default);

        await _repo.Received(1).ListPagedAsync(
            Arg.Any<int>(), Arg.Any<int>(),
            "alice", Arg.Any<string?>(), Arg.Any<bool?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_RoleFilter_PassedToRepository()
    {
        _repo.ListPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool?>(), Arg.Any<CancellationToken>())
            .Returns((new List<User>(), 0));

        await _sut.Handle(new GetUsersQuery(Role: "Auditor"), default);

        await _repo.Received(1).ListPagedAsync(
            Arg.Any<int>(), Arg.Any<int>(),
            Arg.Any<string?>(), "Auditor", null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoFilter_PassesNullFilters()
    {
        _repo.ListPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<bool?>(), Arg.Any<CancellationToken>())
            .Returns((new List<User>(), 0));

        await _sut.Handle(new GetUsersQuery(), default);

        await _repo.Received(1).ListPagedAsync(
            Arg.Any<int>(), Arg.Any<int>(),
            null, null, null, Arg.Any<CancellationToken>());
    }
}
