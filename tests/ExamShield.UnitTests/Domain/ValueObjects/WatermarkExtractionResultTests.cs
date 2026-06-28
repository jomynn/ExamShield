using ExamShield.Domain.ValueObjects;
using FluentAssertions;

namespace ExamShield.UnitTests.Domain.ValueObjects;

public sealed class WatermarkExtractionResultTests
{
    [Fact]
    public void Success_SetsIsValidTrue()
    {
        var payload = new WatermarkPayload { ExamId = Guid.NewGuid(), ImageHash = "abc" };
        var result = WatermarkExtractionResult.Success(payload, 1024);

        result.IsValid.Should().BeTrue();
        result.Payload.Should().BeSameAs(payload);
        result.OriginalImageLength.Should().Be(1024);
    }

    [Fact]
    public void Failure_SetsIsValidFalse()
    {
        var result = WatermarkExtractionResult.Failure();

        result.IsValid.Should().BeFalse();
        result.Payload.Should().BeNull();
        result.OriginalImageLength.Should().Be(0);
    }

    [Fact]
    public void Success_PayloadFieldsAreAccessible()
    {
        var examId = Guid.NewGuid();
        var captureId = Guid.NewGuid();
        var payload = new WatermarkPayload
        {
            ExamId = examId,
            CaptureId = captureId,
            ImageHash = "hash123",
            Nonce = "nonce-abc",
        };
        var result = WatermarkExtractionResult.Success(payload, 512);

        result.Payload!.ExamId.Should().Be(examId);
        result.Payload.CaptureId.Should().Be(captureId);
        result.Payload.ImageHash.Should().Be("hash123");
    }

    [Fact]
    public void TwoFailures_AreIndependent()
    {
        var a = WatermarkExtractionResult.Failure();
        var b = WatermarkExtractionResult.Failure();

        a.Should().NotBeSameAs(b);
        a.IsValid.Should().BeFalse();
        b.IsValid.Should().BeFalse();
    }
}
