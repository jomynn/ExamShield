using ExamShield.Application.Queries.GetExamStatistics;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using NSubstitute;

namespace ExamShield.UnitTests.Application;

public sealed class GetExamStatisticsQueryHandlerTests
{
    private static Score MakeScore(Guid examId, int correct, int total)
    {
        var wrong   = total - correct;
        var key     = Enumerable.Range(1, total).ToDictionary(i => i, i => "A");
        var answers = Enumerable.Range(1, correct)
            .Select(i => new ExtractedAnswer(i, "A", new OcrConfidence(1.0)))
            .Concat(Enumerable.Range(correct + 1, wrong)
                .Select(i => new ExtractedAnswer(i, "B", new OcrConfidence(1.0))))
            .ToList<ExtractedAnswer>();
        return Score.Create(CaptureId.New(), new ExamId(examId), StudentId.New(), answers, new AnswerKey(key));
    }

    private static IScoreRepository RepoWith(Guid examId, IEnumerable<Score> scores)
    {
        var repo = Substitute.For<IScoreRepository>();
        repo.GetByExamIdAsync(new ExamId(examId), default)
            .ReturnsForAnyArgs(Task.FromResult<IReadOnlyList<Score>>(scores.ToList()));
        return repo;
    }

    [Fact]
    public async Task Handle_NoScores_ReturnsZeroStats()
    {
        var examId = Guid.NewGuid();
        var sut    = new GetExamStatisticsQueryHandler(RepoWith(examId, []));

        var result = await sut.Handle(new GetExamStatisticsQuery(examId), default);

        Assert.Equal(0, result.TotalStudents);
        Assert.Equal(0, result.AveragePercentage);
        Assert.Equal(0, result.PassRate);
    }

    [Fact]
    public async Task Handle_AllPassing_PassRateIs100()
    {
        var examId = Guid.NewGuid();
        var scores = Enumerable.Range(0, 5).Select(_ => MakeScore(examId, 8, 10)); // 80% each
        var sut    = new GetExamStatisticsQueryHandler(RepoWith(examId, scores));

        var result = await sut.Handle(new GetExamStatisticsQuery(examId), default);

        Assert.Equal(5, result.TotalStudents);
        Assert.Equal(100.0, result.PassRate);
        Assert.Equal(80.0, result.AveragePercentage);
    }

    [Fact]
    public async Task Handle_MixedScores_ComputesCorrectPassRate()
    {
        var examId = Guid.NewGuid();
        var scores = new[]
        {
            MakeScore(examId, 9, 10),  // 90% → A, pass
            MakeScore(examId, 7, 10),  // 70% → C, pass
            MakeScore(examId, 5, 10),  // 50% → F, fail
            MakeScore(examId, 4, 10),  // 40% → F, fail
        };
        var sut = new GetExamStatisticsQueryHandler(RepoWith(examId, scores));

        var result = await sut.Handle(new GetExamStatisticsQuery(examId), default);

        Assert.Equal(4, result.TotalStudents);
        Assert.Equal(50.0, result.PassRate);
        Assert.Equal(62.5, result.AveragePercentage);
    }

    [Fact]
    public async Task Handle_GradeDistribution_CountsCorrectly()
    {
        var examId = Guid.NewGuid();
        var scores = new[]
        {
            MakeScore(examId, 10, 10), // 100% → A
            MakeScore(examId,  8, 10), //  80% → B
            MakeScore(examId,  7, 10), //  70% → C
            MakeScore(examId,  6, 10), //  60% → D
            MakeScore(examId,  5, 10), //  50% → F
        };
        var sut = new GetExamStatisticsQueryHandler(RepoWith(examId, scores));

        var result = await sut.Handle(new GetExamStatisticsQuery(examId), default);

        Assert.Equal(1, result.GradeDistribution["A"]);
        Assert.Equal(1, result.GradeDistribution["B"]);
        Assert.Equal(1, result.GradeDistribution["C"]);
        Assert.Equal(1, result.GradeDistribution["D"]);
        Assert.Equal(1, result.GradeDistribution["F"]);
    }

    [Fact]
    public async Task Handle_ReturnsHighestAndLowestPercentage()
    {
        var examId = Guid.NewGuid();
        var scores = new[]
        {
            MakeScore(examId, 9, 10),
            MakeScore(examId, 3, 10),
            MakeScore(examId, 6, 10),
        };
        var sut = new GetExamStatisticsQueryHandler(RepoWith(examId, scores));

        var result = await sut.Handle(new GetExamStatisticsQuery(examId), default);

        Assert.Equal(90.0, result.HighestPercentage);
        Assert.Equal(30.0, result.LowestPercentage);
    }
}
