using ExamShield.Application.Queries.GetResults;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetResults;

public sealed class GetResultsQueryHandlerTests
{
    private readonly IScoreRepository _scores = Substitute.For<IScoreRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly GetResultsQueryHandler _sut;

    public GetResultsQueryHandlerTests() => _sut = new(_scores, _cache);

    private static Score MakeScore(Guid examId, int correct = 8, int total = 10)
    {
        var captureId = new CaptureId(Guid.NewGuid());
        var key = new AnswerKey(Enumerable.Range(1, total)
            .ToDictionary(i => i, i => "A"));
        var answers = Enumerable.Range(1, correct)
            .Select(i => new ExtractedAnswer(i, "A", new OcrConfidence(0.9)))
            .Concat(Enumerable.Range(correct + 1, total - correct)
                .Select(i => new ExtractedAnswer(i, "X", new OcrConfidence(0.9))))
            .ToArray();
        var score = Score.Create(captureId, new ExamId(examId), new StudentId(Guid.NewGuid()), answers, key);
        score.Publish();
        return score;
    }

    [Fact]
    public async Task Handle_CacheHit_ReturnsCachedResult()
    {
        var cached = new GetResultsResult([]);
        _cache.GetAsync<GetResultsResult>(Arg.Any<string>(), default).Returns(cached);

        var result = await _sut.Handle(new(), default);

        result.Should().BeSameAs(cached);
        await _scores.DidNotReceive().GetPublishedAsync(default);
    }

    [Fact]
    public async Task Handle_CacheMiss_QueriesScores()
    {
        _cache.GetAsync<GetResultsResult>(Arg.Any<string>(), default).Returns((GetResultsResult?)null);
        var examId = Guid.NewGuid();
        IReadOnlyList<Score> list = [MakeScore(examId)];
        _scores.GetPublishedAsync(default).Returns(list);

        var result = await _sut.Handle(new(), default);

        result.Results.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WithExamIdFilter_ReturnsOnlyMatchingScores()
    {
        _cache.GetAsync<GetResultsResult>(Arg.Any<string>(), default).Returns((GetResultsResult?)null);
        var targetExam = Guid.NewGuid();
        var otherExam = Guid.NewGuid();
        IReadOnlyList<Score> list = [MakeScore(targetExam), MakeScore(otherExam)];
        _scores.GetPublishedAsync(default).Returns(list);

        var result = await _sut.Handle(new(targetExam), default);

        result.Results.Should().HaveCount(1);
        result.Results[0].ExamId.Should().Be(targetExam);
    }

    [Fact]
    public async Task Handle_CacheMiss_CachesResult()
    {
        _cache.GetAsync<GetResultsResult>(Arg.Any<string>(), default).Returns((GetResultsResult?)null);
        IReadOnlyList<Score> empty = [];
        _scores.GetPublishedAsync(default).Returns(empty);

        await _sut.Handle(new(), default);

        await _cache.Received(1).SetAsync(
            Arg.Any<string>(), Arg.Any<GetResultsResult>(),
            TimeSpan.FromMinutes(5), default);
    }

    [Fact]
    public async Task Handle_MapsScoreDtoFields()
    {
        _cache.GetAsync<GetResultsResult>(Arg.Any<string>(), default).Returns((GetResultsResult?)null);
        var examId = Guid.NewGuid();
        var score = MakeScore(examId, correct: 7, total: 10);
        IReadOnlyList<Score> list = [score];
        _scores.GetPublishedAsync(default).Returns(list);

        var result = await _sut.Handle(new(), default);

        var dto = result.Results[0];
        dto.CorrectAnswers.Should().Be(7);
        dto.TotalQuestions.Should().Be(10);
        dto.Percentage.Should().Be(70.0);
    }
}
