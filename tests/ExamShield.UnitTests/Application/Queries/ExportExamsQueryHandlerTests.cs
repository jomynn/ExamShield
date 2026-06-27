using ExamShield.Application.Queries.ExportExams;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries;

public sealed class ExportExamsQueryHandlerTests
{
    private readonly IExamRepository _repo = Substitute.For<IExamRepository>();
    private readonly ExportExamsQueryHandler _sut;

    public ExportExamsQueryHandlerTests() => _sut = new ExportExamsQueryHandler(_repo);

    [Fact]
    public async Task Handle_NoExams_ReturnsCsvHeaderOnly()
    {
        _repo.ListAllAsync(Arg.Any<CancellationToken>()).Returns([]);

        var result = await _sut.Handle(new ExportExamsQuery(), default);

        var lines = result.Csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(lines);
        Assert.Contains("ExamId", lines[0]);
        Assert.Contains("Name", lines[0]);
        Assert.Contains("Status", lines[0]);
        Assert.Contains("TotalQuestions", lines[0]);
    }

    [Fact]
    public async Task Handle_WithExams_ReturnsOneRowPerExam()
    {
        var exams = new[] { MakeExam("Math"), MakeExam("Physics") };
        _repo.ListAllAsync(Arg.Any<CancellationToken>()).Returns(exams);

        var result = await _sut.Handle(new ExportExamsQuery(), default);

        var lines = result.Csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length); // header + 2 rows
    }

    [Fact]
    public async Task Handle_WithStatusFilter_FiltersExams()
    {
        var active = MakeExam("Math");   // Activated in MakeExam
        var draft  = Exam.Create("Physics", null, 10);  // stays Draft
        _repo.ListAllAsync(Arg.Any<CancellationToken>()).Returns([active, draft]);

        var result = await _sut.Handle(new ExportExamsQuery(Status: ExamStatus.Active), default);

        var lines = result.Csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length); // header + 1 Active row
        Assert.DoesNotContain("Physics", result.Csv);
    }

    [Fact]
    public async Task Handle_WithSearchFilter_FiltersExams()
    {
        var exams = new[] { MakeExam("Mathematics"), MakeExam("Physics") };
        _repo.ListAllAsync(Arg.Any<CancellationToken>()).Returns(exams);

        var result = await _sut.Handle(new ExportExamsQuery(Search: "Math"), default);

        var lines = result.Csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(2, lines.Length); // header + Mathematics
        Assert.Contains("Mathematics", result.Csv);
        Assert.DoesNotContain("Physics", result.Csv);
    }

    [Fact]
    public async Task Handle_FilenameIncludesTimestamp()
    {
        _repo.ListAllAsync(Arg.Any<CancellationToken>()).Returns([]);

        var result = await _sut.Handle(new ExportExamsQuery(), default);

        Assert.StartsWith("exams-", result.Filename);
        Assert.EndsWith(".csv", result.Filename);
    }

    private static Exam MakeExam(string name)
    {
        var exam = Exam.Create(name, null, 10);
        exam.Activate();
        return exam;
    }
}
