using ExamShield.Application.Queries.GetStatistics;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetStatistics;

public sealed class GetStatisticsQueryHandlerTests
{
    private readonly IScoreRepository _scores = Substitute.For<IScoreRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly GetStatisticsQueryHandler _sut;

    public GetStatisticsQueryHandlerTests() => _sut = new(_scores, _cache);

    private static Score MakeScore(int correct, int total)
    {
        var key = new AnswerKey(Enumerable.Range(1, total).ToDictionary(i => i, _ => "A"));
        var answers = Enumerable.Range(1, correct)
            .Select(i => new ExtractedAnswer(i, "A", new OcrConfidence(0.9)))
            .Concat(Enumerable.Range(correct + 1, total - correct)
                .Select(i => new ExtractedAnswer(i, "X", new OcrConfidence(0.9))))
            .ToArray();
        return Score.Create(
            new CaptureId(Guid.NewGuid()),
            new ExamId(Guid.NewGuid()),
            new StudentId(Guid.NewGuid()),
            answers, key);
    }

    [Fact]
    public async Task Handle_CacheHit_ReturnsCached()
    {
        var cached = new GetStatisticsResult(5, 80.0, 10, 6);
        _cache.GetAsync<GetStatisticsResult>(Arg.Any<string>(), default).Returns(cached);

        var result = await _sut.Handle(new(), default);

        result.Should().BeSameAs(cached);
        await _scores.DidNotReceive().GetAllAsync(default);
    }

    [Fact]
    public async Task Handle_NoScores_ReturnsZeros()
    {
        _cache.GetAsync<GetStatisticsResult>(Arg.Any<string>(), default).Returns((GetStatisticsResult?)null);
        IReadOnlyList<Score> empty = [];
        _scores.GetAllAsync(default).Returns(empty);

        var result = await _sut.Handle(new(), default);

        result.TotalPapersScored.Should().Be(0);
        result.AveragePercentage.Should().Be(0.0);
        result.HighestScore.Should().Be(0);
        result.LowestScore.Should().Be(0);
    }

    [Fact]
    public async Task Handle_MultipleScores_ComputesStats()
    {
        _cache.GetAsync<GetStatisticsResult>(Arg.Any<string>(), default).Returns((GetStatisticsResult?)null);
        IReadOnlyList<Score> list = [MakeScore(8, 10), MakeScore(6, 10), MakeScore(10, 10)];
        _scores.GetAllAsync(default).Returns(list);

        var result = await _sut.Handle(new(), default);

        result.TotalPapersScored.Should().Be(3);
        result.AveragePercentage.Should().BeApproximately(80.0, 0.01);
        result.HighestScore.Should().Be(10);
        result.LowestScore.Should().Be(6);
    }

    [Fact]
    public async Task Handle_CacheMiss_StoresResult()
    {
        _cache.GetAsync<GetStatisticsResult>(Arg.Any<string>(), default).Returns((GetStatisticsResult?)null);
        IReadOnlyList<Score> empty = [];
        _scores.GetAllAsync(default).Returns(empty);

        await _sut.Handle(new(), default);

        await _cache.Received(1).SetAsync(
            Arg.Any<string>(), Arg.Any<GetStatisticsResult>(),
            TimeSpan.FromMinutes(2), default);
    }
}
