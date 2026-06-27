using ExamShield.Application;
using ExamShield.Application.Commands.PublishResults;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace ExamShield.UnitTests.Application.Commands;

public sealed class PublishResultsCommandHandlerTests
{
    private readonly IScoreRepository _scores = Substitute.For<IScoreRepository>();
    private readonly IAuditLogRepository _auditLog = Substitute.For<IAuditLogRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly PublishResultsCommandHandler _sut;

    public PublishResultsCommandHandlerTests() =>
        _sut = new PublishResultsCommandHandler(_scores, _auditLog, _cache);

    private static Score BuildScore(ExamId examId) =>
        Score.Create(CaptureId.New(), examId, StudentId.New(),
            [new ExtractedAnswer(1, "A", new OcrConfidence(0.9))],
            new AnswerKey(new Dictionary<int, string> { [1] = "A" }));

    [Fact]
    public async Task Handle_PublishesAllUnpublishedScoresForExam()
    {
        var examId = ExamId.New();
        var scores = new List<Score> { BuildScore(examId), BuildScore(examId) };
        _scores.GetByExamIdAsync(examId, Arg.Any<CancellationToken>()).Returns(scores);

        var result = await _sut.Handle(new PublishResultsCommand(examId.Value), default);

        result.PublishedCount.Should().Be(2);
        scores.Should().AllSatisfy(s => s.IsPublished.Should().BeTrue());
    }

    [Fact]
    public async Task Handle_PersistsEachPublishedScore()
    {
        var examId = ExamId.New();
        var scores = new List<Score> { BuildScore(examId), BuildScore(examId) };
        _scores.GetByExamIdAsync(examId, Arg.Any<CancellationToken>()).Returns(scores);

        await _sut.Handle(new PublishResultsCommand(examId.Value), default);

        await _scores.Received(2).UpdateAsync(Arg.Any<Score>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SkipsAlreadyPublishedScores()
    {
        var examId = ExamId.New();
        var published = BuildScore(examId);
        published.Publish();
        var unpublished = BuildScore(examId);
        _scores.GetByExamIdAsync(examId, Arg.Any<CancellationToken>())
            .Returns(new List<Score> { published, unpublished });

        var result = await _sut.Handle(new PublishResultsCommand(examId.Value), default);

        result.PublishedCount.Should().Be(1);
        await _scores.Received(1).UpdateAsync(Arg.Any<Score>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenNoScores_ThrowsNoScoresToPublishException()
    {
        var examId = ExamId.New();
        _scores.GetByExamIdAsync(examId, Arg.Any<CancellationToken>())
            .Returns(new List<Score>());

        await Assert.ThrowsAsync<NoScoresToPublishException>(() =>
            _sut.Handle(new PublishResultsCommand(examId.Value), default));
    }

    [Fact]
    public async Task Handle_WhenPublishing_InvalidatesResultsAndStatisticsCache()
    {
        var examId = ExamId.New();
        _scores.GetByExamIdAsync(examId, Arg.Any<CancellationToken>())
            .Returns(new List<Score> { BuildScore(examId) });

        await _sut.Handle(new PublishResultsCommand(examId.Value), default);

        await _cache.Received(1).InvalidateAsync(CacheKeys.PublishedResults, Arg.Any<CancellationToken>());
        await _cache.Received(1).InvalidateAsync(CacheKeys.Statistics, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAllAlreadyPublished_ThrowsResultsAlreadyPublishedException()
    {
        var examId = ExamId.New();
        var published = BuildScore(examId);
        published.Publish();
        _scores.GetByExamIdAsync(examId, Arg.Any<CancellationToken>())
            .Returns(new List<Score> { published });

        await Assert.ThrowsAsync<ResultsAlreadyPublishedException>(() =>
            _sut.Handle(new PublishResultsCommand(examId.Value), default));
    }
}
