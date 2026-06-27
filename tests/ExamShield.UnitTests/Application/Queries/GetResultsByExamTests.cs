using ExamShield.Application.Queries.GetResults;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries;

public sealed class GetResultsByExamTests
{
    private readonly IScoreRepository _scores = Substitute.For<IScoreRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly GetResultsQueryHandler _sut;

    public GetResultsByExamTests() => _sut = new GetResultsQueryHandler(_scores, _cache);

    private static Score BuildPublishedScore(Guid examId)
    {
        var captureId = CaptureId.New();
        var score = Score.Create(
            captureId, new ExamId(examId), new StudentId(Guid.NewGuid()),
            new List<ExtractedAnswer> { new(1, "A", new OcrConfidence(1.0)) },
            new AnswerKey(new Dictionary<int, string> { [1] = "A" }));
        score.Publish();
        return score;
    }

    [Fact]
    public async Task Handle_WithExamId_ReturnsOnlyThatExam()
    {
        var examId = Guid.NewGuid();
        var scoreA = BuildPublishedScore(examId);
        var scoreB = BuildPublishedScore(Guid.NewGuid());
        _scores.GetPublishedAsync(default).Returns(new List<Score> { scoreA, scoreB });
        _cache.GetAsync<GetResultsResult>(Arg.Any<string>(), default).Returns((GetResultsResult?)null);

        var result = await _sut.Handle(new GetResultsQuery(ExamId: examId), default);

        Assert.Single(result.Results);
        Assert.Equal(examId, result.Results[0].ExamId);
    }

    [Fact]
    public async Task Handle_WithoutExamId_ReturnsAllPublished()
    {
        var scoreA = BuildPublishedScore(Guid.NewGuid());
        var scoreB = BuildPublishedScore(Guid.NewGuid());
        _scores.GetPublishedAsync(default).Returns(new List<Score> { scoreA, scoreB });
        _cache.GetAsync<GetResultsResult>(Arg.Any<string>(), default).Returns((GetResultsResult?)null);

        var result = await _sut.Handle(new GetResultsQuery(), default);

        Assert.Equal(2, result.Results.Count);
    }

    [Fact]
    public async Task Handle_WithExamId_CacheKeyIsExamSpecific()
    {
        var examId = Guid.NewGuid();
        _scores.GetPublishedAsync(default).Returns(new List<Score>());
        _cache.GetAsync<GetResultsResult>(Arg.Any<string>(), default).Returns((GetResultsResult?)null);

        await _sut.Handle(new GetResultsQuery(ExamId: examId), default);

        await _cache.Received(1).SetAsync(
            Arg.Is<string>(k => k.Contains(examId.ToString())),
            Arg.Any<GetResultsResult>(),
            Arg.Any<TimeSpan>(), default);
    }
}
