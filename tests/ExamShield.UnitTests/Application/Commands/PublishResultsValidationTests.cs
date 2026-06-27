using ExamShield.Application.Commands.PublishResults;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands;

public sealed class PublishResultsValidationTests
{
    private readonly IScoreRepository _scores = Substitute.For<IScoreRepository>();
    private readonly IAuditLogRepository _auditLog = Substitute.For<IAuditLogRepository>();
    private readonly ICacheService _cache = Substitute.For<ICacheService>();
    private readonly PublishResultsCommandHandler _sut;

    public PublishResultsValidationTests() =>
        _sut = new PublishResultsCommandHandler(_scores, _auditLog, _cache);

    private static Score BuildScore(bool published = false)
    {
        var score = Score.Create(
            CaptureId.New(), new ExamId(Guid.NewGuid()), new StudentId(Guid.NewGuid()),
            new List<ExtractedAnswer> { new(1, "A", new OcrConfidence(1.0)) },
            new AnswerKey(new Dictionary<int, string> { [1] = "A" }));
        if (published) score.Publish();
        return score;
    }

    [Fact]
    public async Task Handle_WhenNoScoresExist_ThrowsNoScoresToPublishException()
    {
        _scores.GetByExamIdAsync(Arg.Any<ExamId>(), default).Returns(new List<Score>());

        await Assert.ThrowsAsync<NoScoresToPublishException>(() =>
            _sut.Handle(new PublishResultsCommand(Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Handle_WhenAllScoresAlreadyPublished_ThrowsResultsAlreadyPublishedException()
    {
        var examId = Guid.NewGuid();
        var published = BuildScore(published: true);
        _scores.GetByExamIdAsync(Arg.Any<ExamId>(), default).Returns(new List<Score> { published });

        await Assert.ThrowsAsync<ResultsAlreadyPublishedException>(() =>
            _sut.Handle(new PublishResultsCommand(examId), default));
    }

    [Fact]
    public async Task Handle_WithUnpublishedScores_PublishesAndReturnsCount()
    {
        var score = BuildScore(published: false);
        _scores.GetByExamIdAsync(Arg.Any<ExamId>(), default).Returns(new List<Score> { score });

        var result = await _sut.Handle(new PublishResultsCommand(Guid.NewGuid()), default);

        Assert.Equal(1, result.PublishedCount);
        Assert.True(score.IsPublished);
    }
}
