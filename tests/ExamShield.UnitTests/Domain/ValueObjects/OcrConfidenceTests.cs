using ExamShield.Domain.ValueObjects;
using FluentAssertions;

namespace ExamShield.UnitTests.Domain.ValueObjects;

public sealed class OcrConfidenceTests
{
    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Constructor_ValidValue_Accepts(double value)
    {
        var confidence = new OcrConfidence(value);
        confidence.Value.Should().Be(value);
    }

    [Theory]
    [InlineData(-0.001)]
    [InlineData(1.001)]
    public void Constructor_OutOfRange_Throws(double value)
    {
        var act = () => new OcrConfidence(value);
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void IsLow_BelowThreshold_ReturnsTrue()
    {
        var c = new OcrConfidence(OcrConfidence.LowThreshold - 0.01);
        c.IsLow.Should().BeTrue();
    }

    [Fact]
    public void IsLow_AtOrAboveThreshold_ReturnsFalse()
    {
        var c = new OcrConfidence(OcrConfidence.LowThreshold);
        c.IsLow.Should().BeFalse();
    }

    [Fact]
    public void Equals_SameValue_ReturnsTrue()
    {
        var a = new OcrConfidence(0.75);
        var b = new OcrConfidence(0.75);
        a.Equals(b).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        var a = new OcrConfidence(0.75);
        var b = new OcrConfidence(0.80);
        a.Equals(b).Should().BeFalse();
    }

    [Fact]
    public void LowThreshold_MatchesSystemSettingsDefault()
    {
        OcrConfidence.LowThreshold.Should().Be(0.85);
    }
}
