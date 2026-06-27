using ExamShield.Application.Queries.GetExams;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries;

public sealed class GetExamsQueryHandlerFilterTests
{
    private readonly IExamRepository _repo = Substitute.For<IExamRepository>();
    private readonly GetExamsQueryHandler _sut;

    public GetExamsQueryHandlerFilterTests() => _sut = new GetExamsQueryHandler(_repo);

    private static (IReadOnlyList<Exam>, int) NoExams() =>
        (Array.Empty<Exam>(), 0);

    [Fact]
    public async Task Handle_WithStatusFilter_PassesStatusToRepository()
    {
        _repo.ListPagedAsync(1, 50, Arg.Any<string?>(), ExamStatus.Active, null, null, Arg.Any<CancellationToken>())
             .Returns(NoExams());

        await _sut.Handle(new GetExamsQuery(1, 50, Status: ExamStatus.Active), default);

        await _repo.Received(1).ListPagedAsync(
            1, 50, null, ExamStatus.Active, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithSearchTerm_PassesSearchToRepository()
    {
        _repo.ListPagedAsync(1, 50, "math", Arg.Any<ExamStatus?>(), null, null, Arg.Any<CancellationToken>())
             .Returns(NoExams());

        await _sut.Handle(new GetExamsQuery(1, 50, Search: "math"), default);

        await _repo.Received(1).ListPagedAsync(
            1, 50, "math", null, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_NoFilters_PassesNullsToRepository()
    {
        _repo.ListPagedAsync(1, 20, null, null, null, null, Arg.Any<CancellationToken>())
             .Returns(NoExams());

        await _sut.Handle(new GetExamsQuery(1, 20), default);

        await _repo.Received(1).ListPagedAsync(
            1, 20, null, null, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithBothFilters_PassesBothToRepository()
    {
        _repo.ListPagedAsync(1, 50, "science", ExamStatus.Draft, null, null, Arg.Any<CancellationToken>())
             .Returns(NoExams());

        await _sut.Handle(new GetExamsQuery(1, 50, Search: "science", Status: ExamStatus.Draft), default);

        await _repo.Received(1).ListPagedAsync(
            1, 50, "science", ExamStatus.Draft, null, null, Arg.Any<CancellationToken>());
    }
}
