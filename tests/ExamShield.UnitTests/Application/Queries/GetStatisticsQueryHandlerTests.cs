using ExamShield.Application;
using ExamShield.Application.Queries.GetStatistics;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace ExamShield.UnitTests.Application.Queries;

public sealed class GetStatisticsQueryHandlerTests
{
    private readonly IScoreRepository _scores = Substitute.For<IScoreRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly GetStatisticsQueryHandler _sut;

    public GetStatisticsQueryHandlerTests() =>
        _sut = new GetStatisticsQueryHandler(_scores, _cache);

    private static Score BuildScore(int correctAnswers, int total)
    {
        var answers = Enumerable.Range(1, total)
            .Select(i => new ExtractedAnswer(i, "A", new OcrConfidence(0.9)))
            .ToList();
        var key = new AnswerKey(Enumerable.Range(1, correctAnswers)
            .ToDictionary(i => i, _ => "A"));
        return Score.Create(CaptureId.New(), ExamId.New(), StudentId.New(), answers, key);
    }

    [Fact]
    public async Task Handle_OnCacheHit_ReturnsCachedResultWithoutHittingRepository()
    {
        var cached = new GetStatisticsResult(5, 80.0, 10, 5);
        _cache.GetAsync<GetStatisticsResult>(CacheKeys.Statistics, Arg.Any<CancellationToken>())
            .Returns(cached);

        var result = await _sut.Handle(new GetStatisticsQuery(), default);

        result.Should().Be(cached);
        await _scores.DidNotReceive().GetAllAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OnCacheMiss_ComputesAndCachesStatistics()
    {
        _cache.GetAsync<GetStatisticsResult>(CacheKeys.Statistics, Arg.Any<CancellationToken>())
            .Returns((GetStatisticsResult?)null);
        _scores.GetAllAsync(Arg.Any<CancellationToken>())
            .Returns(new List<Score> { BuildScore(3, 3), BuildScore(2, 3) });

        var result = await _sut.Handle(new GetStatisticsQuery(), default);

        result.TotalPapersScored.Should().Be(2);
        await _cache.Received(1).SetAsync(
            CacheKeys.Statistics,
            Arg.Any<GetStatisticsResult>(),
            TimeSpan.FromMinutes(2),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OnCacheMiss_WhenNoScores_ReturnsZeroStats()
    {
        _cache.GetAsync<GetStatisticsResult>(CacheKeys.Statistics, Arg.Any<CancellationToken>())
            .Returns((GetStatisticsResult?)null);
        _scores.GetAllAsync(Arg.Any<CancellationToken>()).Returns(new List<Score>());

        var result = await _sut.Handle(new GetStatisticsQuery(), default);

        result.TotalPapersScored.Should().Be(0);
        result.AveragePercentage.Should().Be(0.0);
    }
}
