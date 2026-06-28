using ExamShield.Application.Queries.GetExamStatistics;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetExamStatistics;

public sealed class GetExamStatisticsQueryHandlerTests
{
    private readonly IScoreRepository _scores = Substitute.For<IScoreRepository>();
    private readonly GetExamStatisticsQueryHandler _sut;

    public GetExamStatisticsQueryHandlerTests() =>
        _sut = new GetExamStatisticsQueryHandler(_scores);

    private static Score MakeScore(ExamId examId, double percentage)
    {
        var answerKey = new AnswerKey(new Dictionary<int, string> { [1] = "A" });
        var answers = new[]
        {
            new ExtractedAnswer(1, percentage >= 100 ? "A" : "B", new OcrConfidence(0.9))
        };
        var s = Score.Create(
            new CaptureId(Guid.NewGuid()), examId, new StudentId(Guid.NewGuid()),
            answers, answerKey);
        // Force percentage via reflection isn't available; create with real domain logic instead
        // Use the actual Score that results from the answer key match
        return s;
    }

    [Fact]
    public async Task Handle_NoScores_ReturnsZeroStats()
    {
        var examId = new ExamId(Guid.NewGuid());
        _scores.GetByExamIdAsync(examId, Arg.Any<CancellationToken>())
               .Returns(Array.Empty<Score>());

        var result = await _sut.Handle(new GetExamStatisticsQuery(examId.Value), default);

        result.TotalStudents.Should().Be(0);
        result.AveragePercentage.Should().Be(0);
        result.PassRate.Should().Be(0);
        result.GradeDistribution.Should().HaveCount(5);
    }

    [Fact]
    public async Task Handle_AllCorrect_Returns100PercentAverage()
    {
        var examId = new ExamId(Guid.NewGuid());
        var answerKey = new AnswerKey(new Dictionary<int, string> { [1] = "A" });
        var answers = new[] { new ExtractedAnswer(1, "A", new OcrConfidence(0.9)) };
        var score = Score.Create(new CaptureId(Guid.NewGuid()), examId,
            new StudentId(Guid.NewGuid()), answers, answerKey);

        _scores.GetByExamIdAsync(examId, Arg.Any<CancellationToken>()).Returns(new[] { score });

        var result = await _sut.Handle(new GetExamStatisticsQuery(examId.Value), default);

        result.TotalStudents.Should().Be(1);
        result.AveragePercentage.Should().Be(100);
        result.HighestPercentage.Should().Be(100);
        result.LowestPercentage.Should().Be(100);
        result.PassRate.Should().Be(100);
        result.GradeDistribution["A"].Should().Be(1);
    }

    [Fact]
    public async Task Handle_AllWrong_Returns0PercentFGrade()
    {
        var examId = new ExamId(Guid.NewGuid());
        var answerKey = new AnswerKey(new Dictionary<int, string> { [1] = "A" });
        var answers = new[] { new ExtractedAnswer(1, "B", new OcrConfidence(0.9)) };
        var score = Score.Create(new CaptureId(Guid.NewGuid()), examId,
            new StudentId(Guid.NewGuid()), answers, answerKey);

        _scores.GetByExamIdAsync(examId, Arg.Any<CancellationToken>()).Returns(new[] { score });

        var result = await _sut.Handle(new GetExamStatisticsQuery(examId.Value), default);

        result.AveragePercentage.Should().Be(0);
        result.PassRate.Should().Be(0);
        result.GradeDistribution["F"].Should().Be(1);
    }

    [Fact]
    public async Task Handle_MixedScores_ComputesGradeDistributionCorrectly()
    {
        var examId = new ExamId(Guid.NewGuid());
        // Build answer keys with multiple questions to get target percentages
        // 10q: 10/10=A(100%), 8/10=B(80%), 7/10=C(70%), 6/10=D(60%), 5/10=F(50%)
        var key = new Dictionary<int, string>();
        for (var i = 1; i <= 10; i++) key[i] = "A";
        var answerKey = new AnswerKey(key);

        Score MakeWithCorrect(int correct)
        {
            var ans = Enumerable.Range(1, 10)
                .Select(i => new ExtractedAnswer(i, i <= correct ? "A" : "B", new OcrConfidence(0.9)))
                .ToList();
            return Score.Create(new CaptureId(Guid.NewGuid()), examId,
                new StudentId(Guid.NewGuid()), ans, answerKey);
        }

        var scores = new[] { MakeWithCorrect(10), MakeWithCorrect(8), MakeWithCorrect(7), MakeWithCorrect(6), MakeWithCorrect(5) };
        _scores.GetByExamIdAsync(examId, Arg.Any<CancellationToken>()).Returns(scores);

        var result = await _sut.Handle(new GetExamStatisticsQuery(examId.Value), default);

        result.TotalStudents.Should().Be(5);
        result.GradeDistribution["A"].Should().Be(1); // 100%
        result.GradeDistribution["B"].Should().Be(1); // 80%
        result.GradeDistribution["C"].Should().Be(1); // 70%
        result.GradeDistribution["D"].Should().Be(1); // 60%
        result.GradeDistribution["F"].Should().Be(1); // 50%
        result.PassRate.Should().Be(80); // 4/5 pass (>=60%)
    }
}
