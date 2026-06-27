using ExamShield.Domain.Entities;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;

namespace ExamShield.UnitTests.Domain.Entities;

public sealed class ExamAnswerKeyValidationTests
{
    private static readonly ExamId AnyExamId = new(Guid.NewGuid());

    [Fact]
    public void Create_WithNonPositiveQuestionNumber_ThrowsArgumentException()
    {
        var answers = new Dictionary<int, string> { [0] = "A", [1] = "B" };

        var act = () => ExamAnswerKey.Create(AnyExamId, answers);

        act.Should().Throw<ArgumentException>().WithMessage("*question number*");
    }

    [Fact]
    public void Create_WithNegativeQuestionNumber_ThrowsArgumentException()
    {
        var answers = new Dictionary<int, string> { [-1] = "A" };

        var act = () => ExamAnswerKey.Create(AnyExamId, answers);

        act.Should().Throw<ArgumentException>().WithMessage("*question number*");
    }

    [Fact]
    public void Create_WithBlankAnswerText_ThrowsArgumentException()
    {
        var answers = new Dictionary<int, string> { [1] = "  " };

        var act = () => ExamAnswerKey.Create(AnyExamId, answers);

        act.Should().Throw<ArgumentException>().WithMessage("*answer*");
    }

    [Fact]
    public void Create_WithEmptyAnswerText_ThrowsArgumentException()
    {
        var answers = new Dictionary<int, string> { [1] = "" };

        var act = () => ExamAnswerKey.Create(AnyExamId, answers);

        act.Should().Throw<ArgumentException>().WithMessage("*answer*");
    }

    [Fact]
    public void Create_WithValidEntries_Succeeds()
    {
        var answers = new Dictionary<int, string> { [1] = "A", [2] = "B", [3] = "C" };

        var key = ExamAnswerKey.Create(AnyExamId, answers);

        key.Answers.Should().HaveCount(3);
    }
}
