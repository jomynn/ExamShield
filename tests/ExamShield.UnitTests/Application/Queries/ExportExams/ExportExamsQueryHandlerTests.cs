using ExamShield.Application.Queries.ExportExams;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.ExportExams;

public sealed class ExportExamsQueryHandlerTests
{
    private readonly IExamRepository _exams = Substitute.For<IExamRepository>();
    private readonly ExportExamsQueryHandler _sut;

    public ExportExamsQueryHandlerTests() => _sut = new(_exams);

    [Fact]
    public async Task Handle_NoFilter_ExportsAll()
    {
        IReadOnlyList<Exam> list = [Exam.Create("Math", null, 10), Exam.Create("Science", null, 20)];
        _exams.ListAllAsync(default).Returns(list);

        var result = await _sut.Handle(new(), default);

        result.Csv.Trim().Split('\n').Should().HaveCount(3); // header + 2 rows
    }

    [Fact]
    public async Task Handle_SearchFilter_OnlyMatchingExams()
    {
        IReadOnlyList<Exam> list = [Exam.Create("Math Final", null, 10), Exam.Create("Physics", null, 20)];
        _exams.ListAllAsync(default).Returns(list);

        var result = await _sut.Handle(new(Search: "math"), default);

        result.Csv.Should().Contain("Math Final");
        result.Csv.Should().NotContain("Physics");
    }

    [Fact]
    public async Task Handle_CsvContainsHeaderColumns()
    {
        IReadOnlyList<Exam> empty = [];
        _exams.ListAllAsync(default).Returns(empty);

        var result = await _sut.Handle(new(), default);

        result.Csv.Should().Contain("ExamId,Name,Description,Status");
    }

    [Fact]
    public async Task Handle_FilenameFormat()
    {
        IReadOnlyList<Exam> empty = [];
        _exams.ListAllAsync(default).Returns(empty);

        var result = await _sut.Handle(new(), default);

        result.Filename.Should().StartWith("exams-");
        result.Filename.Should().EndWith(".csv");
    }
}
