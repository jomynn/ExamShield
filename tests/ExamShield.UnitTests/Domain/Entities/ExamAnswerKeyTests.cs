using ExamShield.Domain.Entities;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;

namespace ExamShield.UnitTests.Domain.Entities;

public sealed class ExamAnswerKeyTests
{
    private static IReadOnlyDictionary<int, string> ValidAnswers() =>
        new Dictionary<int, string> { [1] = "A", [2] = "B", [3] = "C" };

    [Fact]
    public void Create_ValidArgs_SetsProperties()
    {
        var examId = ExamId.New();
        var key = ExamAnswerKey.Create(examId, ValidAnswers());

        key.ExamId.Should().Be(examId);
        key.Answers.Should().HaveCount(3);
        key.CreatedAt.Should().BeCloseTo(DateTimeOffset.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_NullExamId_ThrowsArgumentNullException()
    {
        var act = () => ExamAnswerKey.Create(null!, ValidAnswers());
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_EmptyAnswers_ThrowsArgumentException()
    {
        var act = () => ExamAnswerKey.Create(ExamId.New(), new Dictionary<int, string>());
        act.Should().Throw<ArgumentException>().WithMessage("*at least one*");
    }

    [Fact]
    public void Create_NullAnswers_ThrowsArgumentException()
    {
        var act = () => ExamAnswerKey.Create(ExamId.New(), null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Create_NegativeQuestionNumber_ThrowsArgumentException()
    {
        var answers = new Dictionary<int, string> { [-1] = "A" };
        var act = () => ExamAnswerKey.Create(ExamId.New(), answers);
        act.Should().Throw<ArgumentException>().WithMessage("*positive*");
    }

    [Fact]
    public void Create_ZeroQuestionNumber_ThrowsArgumentException()
    {
        var answers = new Dictionary<int, string> { [0] = "A" };
        var act = () => ExamAnswerKey.Create(ExamId.New(), answers);
        act.Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_EmptyAnswerText_ThrowsArgumentException(string text)
    {
        var answers = new Dictionary<int, string> { [1] = text };
        var act = () => ExamAnswerKey.Create(ExamId.New(), answers);
        act.Should().Throw<ArgumentException>().WithMessage("*empty*");
    }

    [Fact]
    public void ToValueObject_ReturnsAnswerKeyWithSameEntries()
    {
        var answers = new Dictionary<int, string> { [1] = "A", [2] = "B" };
        var key = ExamAnswerKey.Create(ExamId.New(), answers);
        var vo = key.ToValueObject();

        vo.Count.Should().Be(2);
        vo.IsCorrect(1, "A").Should().BeTrue();
        vo.IsCorrect(2, "B").Should().BeTrue();
    }
}
