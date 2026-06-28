using ExamShield.Application.Queries.GetStudentResults;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetStudentResults;

public sealed class GetStudentResultsQueryHandlerTests
{
    private readonly ICaptureRepository _captures = Substitute.For<ICaptureRepository>();
    private readonly IScoreRepository _scores = Substitute.For<IScoreRepository>();
    private readonly IExamRepository _exams = Substitute.For<IExamRepository>();
    private readonly GetStudentResultsQueryHandler _sut;

    public GetStudentResultsQueryHandlerTests() => _sut = new(_captures, _scores, _exams);

    private static Score MakeScore(Guid studentId, Guid examId, int correct = 8, int total = 10)
    {
        var key = new AnswerKey(Enumerable.Range(1, total).ToDictionary(i => i, _ => "A"));
        var answers = Enumerable.Range(1, correct)
            .Select(i => new ExtractedAnswer(i, "A", new OcrConfidence(0.9)))
            .Concat(Enumerable.Range(correct + 1, total - correct)
                .Select(i => new ExtractedAnswer(i, "X", new OcrConfidence(0.9))))
            .ToArray();
        var score = Score.Create(
            new CaptureId(Guid.NewGuid()),
            new ExamId(examId),
            new StudentId(studentId),
            answers, key);
        score.Publish();
        return score;
    }

    [Fact]
    public async Task Handle_NoScores_ReturnsEmptyResults()
    {
        var studentId = Guid.NewGuid();
        IReadOnlyList<Score> empty = [];
        _scores.GetPublishedAsync(default).Returns(empty);

        var result = await _sut.Handle(new(studentId), default);

        result.StudentId.Should().Be(studentId);
        result.Results.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_FiltersByStudentId()
    {
        var studentA = Guid.NewGuid();
        var studentB = Guid.NewGuid();
        var examId = Guid.NewGuid();
        IReadOnlyList<Score> scores = [MakeScore(studentA, examId), MakeScore(studentB, examId)];
        _scores.GetPublishedAsync(default).Returns(scores);
        IReadOnlyList<Capture> caps = [];
        _captures.ListByStudentIdAsync(Arg.Any<StudentId>(), default).Returns(caps);
        IReadOnlyList<Exam> exams = [];
        _exams.ListAllAsync(default).Returns(exams);

        var result = await _sut.Handle(new(studentA), default);

        result.Results.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_UsesExamNameWhenAvailable()
    {
        var studentId = Guid.NewGuid();
        var examId = Guid.NewGuid();
        var score = MakeScore(studentId, examId);
        IReadOnlyList<Score> scores = [score];
        _scores.GetPublishedAsync(default).Returns(scores);

        var exam = Exam.Create("Chemistry Final", null, 10);
        IReadOnlyList<Exam> examList = [exam];
        _exams.ListAllAsync(default).Returns(examList);
        IReadOnlyList<Capture> caps = [];
        _captures.ListByStudentIdAsync(Arg.Any<StudentId>(), default).Returns(caps);

        // Override score.ExamId to match exam.Id — use a score with the exam's actual Id
        var key = new AnswerKey(Enumerable.Range(1, 10).ToDictionary(i => i, _ => "A"));
        var answers = Enumerable.Range(1, 8)
            .Select(i => new ExtractedAnswer(i, "A", new OcrConfidence(0.9)))
            .Concat(Enumerable.Range(9, 2).Select(i => new ExtractedAnswer(i, "X", new OcrConfidence(0.9))))
            .ToArray();
        var matchedScore = Score.Create(
            new CaptureId(Guid.NewGuid()), exam.Id, new StudentId(studentId), answers, key);
        matchedScore.Publish();
        IReadOnlyList<Score> matchedScores = [matchedScore];
        _scores.GetPublishedAsync(default).Returns(matchedScores);

        var result = await _sut.Handle(new(studentId), default);

        result.Results[0].ExamName.Should().Be("Chemistry Final");
    }

    [Fact]
    public async Task Handle_MapsPercentageCorrectly()
    {
        var studentId = Guid.NewGuid();
        var score = MakeScore(studentId, Guid.NewGuid(), correct: 6, total: 10);
        IReadOnlyList<Score> scores = [score];
        _scores.GetPublishedAsync(default).Returns(scores);
        IReadOnlyList<Capture> caps = [];
        _captures.ListByStudentIdAsync(Arg.Any<StudentId>(), default).Returns(caps);
        IReadOnlyList<Exam> exams = [];
        _exams.ListAllAsync(default).Returns(exams);

        var result = await _sut.Handle(new(studentId), default);

        result.Results[0].Percentage.Should().Be(60.0);
        result.Results[0].CorrectAnswers.Should().Be(6);
        result.Results[0].TotalQuestions.Should().Be(10);
    }
}
