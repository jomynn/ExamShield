using ExamShield.Application;
using ExamShield.Application.Queries.GetResults;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace ExamShield.UnitTests.Application.Queries;

public sealed class GetResultsQueryHandlerTests
{
    private readonly IScoreRepository _scores = Substitute.For<IScoreRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly GetResultsQueryHandler _sut;

    public GetResultsQueryHandlerTests() =>
        _sut = new GetResultsQueryHandler(_scores, _cache);

    private static Score BuildPublishedScore()
    {
        var score = Score.Create(
            CaptureId.New(), ExamId.New(), StudentId.New(),
            [new ExtractedAnswer(1, "A", new OcrConfidence(0.9))],
            new AnswerKey(new Dictionary<int, string> { [1] = "A" }));
        score.Publish();
        return score;
    }

    [Fact]
    public async Task Handle_OnCacheHit_ReturnsCachedResultWithoutHittingRepository()
    {
        var cached = new GetResultsResult(new List<ScoreDto>());
        _cache.GetAsync<GetResultsResult>(CacheKeys.PublishedResults, Arg.Any<CancellationToken>())
            .Returns(cached);

        var result = await _sut.Handle(new GetResultsQuery(), default);

        result.Should().BeSameAs(cached);
        await _scores.DidNotReceive().GetPublishedAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OnCacheMiss_QueriesRepositoryAndPopulatesCache()
    {
        _cache.GetAsync<GetResultsResult>(CacheKeys.PublishedResults, Arg.Any<CancellationToken>())
            .Returns((GetResultsResult?)null);
        var scores = new List<Score> { BuildPublishedScore() };
        _scores.GetPublishedAsync(Arg.Any<CancellationToken>()).Returns(scores);

        var result = await _sut.Handle(new GetResultsQuery(), default);

        result.Results.Should().HaveCount(1);
        await _cache.Received(1).SetAsync(
            CacheKeys.PublishedResults,
            Arg.Any<GetResultsResult>(),
            TimeSpan.FromMinutes(5),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_OnCacheMiss_WithNoScores_ReturnsEmptyList()
    {
        _cache.GetAsync<GetResultsResult>(CacheKeys.PublishedResults, Arg.Any<CancellationToken>())
            .Returns((GetResultsResult?)null);
        _scores.GetPublishedAsync(Arg.Any<CancellationToken>()).Returns(new List<Score>());

        var result = await _sut.Handle(new GetResultsQuery(), default);

        result.Results.Should().BeEmpty();
    }
}
