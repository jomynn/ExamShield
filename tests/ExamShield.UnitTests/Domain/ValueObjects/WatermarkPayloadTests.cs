using ExamShield.Domain.ValueObjects;
using FluentAssertions;

namespace ExamShield.UnitTests.Domain.ValueObjects;

public sealed class WatermarkPayloadTests
{
    [Fact]
    public void Init_SetsAllProperties()
    {
        var examId = Guid.NewGuid();
        var captureId = Guid.NewGuid();
        var deviceId = Guid.NewGuid();
        var ticks = DateTimeOffset.UtcNow.UtcTicks;

        var payload = new WatermarkPayload
        {
            ExamId = examId,
            CaptureId = captureId,
            DeviceId = deviceId,
            TimestampUtcTicks = ticks,
            Nonce = "abc",
            ImageHash = "deadbeef",
        };

        payload.ExamId.Should().Be(examId);
        payload.CaptureId.Should().Be(captureId);
        payload.DeviceId.Should().Be(deviceId);
        payload.TimestampUtcTicks.Should().Be(ticks);
        payload.Nonce.Should().Be("abc");
        payload.ImageHash.Should().Be("deadbeef");
    }

    [Fact]
    public void DefaultNonce_IsEmptyString()
    {
        var payload = new WatermarkPayload();
        payload.Nonce.Should().Be(string.Empty);
    }

    [Fact]
    public void DefaultImageHash_IsEmptyString()
    {
        var payload = new WatermarkPayload();
        payload.ImageHash.Should().Be(string.Empty);
    }

    [Fact]
    public void RecordEquality_SameValues_AreEqual()
    {
        var id = Guid.NewGuid();
        var a = new WatermarkPayload { ExamId = id, ImageHash = "h" };
        var b = new WatermarkPayload { ExamId = id, ImageHash = "h" };
        a.Should().Be(b);
    }

    [Fact]
    public void RecordEquality_DifferentValues_AreNotEqual()
    {
        var a = new WatermarkPayload { ExamId = Guid.NewGuid(), ImageHash = "h1" };
        var b = new WatermarkPayload { ExamId = Guid.NewGuid(), ImageHash = "h2" };
        a.Should().NotBe(b);
    }
}
