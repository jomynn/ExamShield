using ExamShield.Domain.ValueObjects;
using FluentAssertions;

namespace ExamShield.UnitTests.Domain.ValueObjects;

public sealed class ReviewedAnswerTests
{
    [Fact]
    public void Constructor_ValidArgs_SetsProperties()
    {
        var answer = new ReviewedAnswer(3, "B");
        answer.QuestionNumber.Should().Be(3);
        answer.Text.Should().Be("B");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_NonPositiveQuestion_ThrowsArgumentOutOfRange(int questionNumber)
    {
        var act = () => new ReviewedAnswer(questionNumber, "A");
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyOrWhitespaceText_ThrowsArgumentException(string text)
    {
        var act = () => new ReviewedAnswer(1, text);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NullText_ThrowsArgumentException()
    {
        var act = () => new ReviewedAnswer(1, null!);
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_LargeQuestionNumber_IsValid()
    {
        var answer = new ReviewedAnswer(100, "D");
        answer.QuestionNumber.Should().Be(100);
    }
}
