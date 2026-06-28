using ExamShield.Application.Queries.GetExams;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetExams;

public sealed class GetExamsQueryHandlerTests
{
    private readonly IExamRepository _exams = Substitute.For<IExamRepository>();
    private readonly GetExamsQueryHandler _sut;

    public GetExamsQueryHandlerTests() => _sut = new(_exams);

    // GetExamsQuery(int Page, int PageSize, string? Search, ExamStatus? Status, DateTimeOffset? ScheduledFrom, DateTimeOffset? ScheduledTo)
    private static GetExamsQuery PageQuery(int page = 1, int size = 10) =>
        new(page, size, null, null, null, null);

    [Fact]
    public async Task Handle_ReturnsPagedExamDtos()
    {
        IReadOnlyList<Exam> list = [Exam.Create("Math", null, 40), Exam.Create("Science", null, 30)];
        _exams.ListPagedAsync(1, 10, null, null, null, null, default).Returns((list, 2));

        var result = await _sut.Handle(PageQuery(), default);

        result.Exams.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_MapsTotalQuestionsAndStatus()
    {
        IReadOnlyList<Exam> list = [Exam.Create("Bio", null, 50)];
        _exams.ListPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(),
            Arg.Any<ExamStatus?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(),
            default).Returns((list, 1));

        var result = await _sut.Handle(PageQuery(), default);

        result.Exams[0].TotalQuestions.Should().Be(50);
        result.Exams[0].Status.Should().Be("Draft");
    }

    [Fact]
    public async Task Handle_EmptyRepo_ReturnsEmptyResult()
    {
        IReadOnlyList<Exam> empty = [];
        _exams.ListPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(),
            Arg.Any<ExamStatus?>(), Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(),
            default).Returns((empty, 0));

        var result = await _sut.Handle(PageQuery(), default);

        result.Exams.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_PropagatesPaginationMeta()
    {
        IReadOnlyList<Exam> list = [Exam.Create("Exam", null, 10)];
        _exams.ListPagedAsync(2, 5, Arg.Any<string?>(), Arg.Any<ExamStatus?>(),
            Arg.Any<DateTimeOffset?>(), Arg.Any<DateTimeOffset?>(), default).Returns((list, 100));

        var result = await _sut.Handle(new(2, 5, null, null, null, null), default);

        result.Page.Should().Be(2);
        result.PageSize.Should().Be(5);
        result.TotalCount.Should().Be(100);
    }
}
