using ExamShield.Domain.ValueObjects;
using FluentAssertions;

namespace ExamShield.UnitTests.Domain;

public sealed class OcrConfidenceTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Constructor_WithValidValue_Stores(double value)
    {
        var confidence = new OcrConfidence(value);
        confidence.Value.Should().Be(value);
    }

    [Fact]
    public void Constructor_WithNegativeValue_Throws()
    {
        var act = () => new OcrConfidence(-0.1);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Constructor_WithValueAboveOne_Throws()
    {
        var act = () => new OcrConfidence(1.001);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(0.84)]
    public void IsLow_BelowThreshold_ReturnsTrue(double value)
    {
        new OcrConfidence(value).IsLow.Should().BeTrue();
    }

    [Theory]
    [InlineData(0.85)]
    [InlineData(0.95)]
    [InlineData(1.0)]
    public void IsLow_AtOrAboveThreshold_ReturnsFalse(double value)
    {
        new OcrConfidence(value).IsLow.Should().BeFalse();
    }

    [Fact]
    public void Equality_SameValue_AreEqual()
    {
        new OcrConfidence(0.9).Should().Be(new OcrConfidence(0.9));
    }
}
