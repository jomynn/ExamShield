using ExamShield.Domain.ValueObjects;
using ExamShield.Infrastructure.Watermark;
using FluentAssertions;
using SkiaSharp;

namespace ExamShield.UnitTests.Infrastructure.Watermark;

public sealed class LsbSteganographyServiceTests
{
    private readonly LsbSteganographyService _sut = new();

    // Build a valid opaque PNG (alpha=255 everywhere) so premultiplied alpha
    // doesn't zero out blue-channel LSBs during embed/extract.
    private static byte[] CreateTestPng(int width = 64, int height = 64)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var bmp    = new SKBitmap(info);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(new SKColor(200, 200, 200, 255));
        using var img  = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    private static WatermarkPayload CreatePayload() => new WatermarkPayload
    {
        ExamId    = Guid.NewGuid(),
        CaptureId = Guid.NewGuid(),
        DeviceId  = Guid.NewGuid(),
        TimestampUtcTicks = DateTimeOffset.UtcNow.Ticks,
        Nonce     = "abc123",
        ImageHash = "deadbeef00112233",
    };

    [Fact]
    public void Embed_ThenExtract_RoundTrips()
    {
        var png     = CreateTestPng();
        var payload = CreatePayload();

        var watermarked = _sut.Embed(png, payload);
        var result      = _sut.Extract(watermarked);

        result.IsValid.Should().BeTrue();
        result.Payload!.ExamId.Should().Be(payload.ExamId);
        result.Payload!.Nonce.Should().Be(payload.Nonce);
        result.Payload!.ImageHash.Should().Be(payload.ImageHash);
    }

    [Fact]
    public void Extract_FromUnwatermarkedPng_ReturnsFailure()
    {
        var plain = CreateTestPng();
        _sut.Extract(plain).IsValid.Should().BeFalse();
    }

    [Fact]
    public void Embed_OutputIsPng()
    {
        var watermarked = _sut.Embed(CreateTestPng(), CreatePayload());
        // PNG magic bytes: 137 80 78 71
        watermarked[0].Should().Be(0x89);
        watermarked[1].Should().Be(0x50);  // 'P'
        watermarked[2].Should().Be(0x4E);  // 'N'
        watermarked[3].Should().Be(0x47);  // 'G'
    }

    [Fact]
    public void Embed_TooSmallImage_Throws()
    {
        using var bmp = new SKBitmap(1, 1);
        using var img = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        var tinyPng = data.ToArray();

        var act = () => _sut.Embed(tinyPng, CreatePayload());
        act.Should().Throw<InvalidOperationException>().WithMessage("*too small*");
    }

    [Fact]
    public void Extract_CorruptedPayloadBits_ReturnsFailure()
    {
        var watermarked = _sut.Embed(CreateTestPng(), CreatePayload());

        // Zero out LSBs of all blue channels — destroys the embedded bits cleanly
        using var bmp = SKBitmap.Decode(watermarked)!;
        for (var y = 0; y < bmp.Height; y++)
            for (var x = 0; x < bmp.Width; x++)
            {
                var px = bmp.GetPixel(x, y);
                bmp.SetPixel(x, y, new SKColor(px.Red, px.Green, (byte)(px.Blue & 0xFE), px.Alpha));
            }
        using var img  = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Png, 100);
        _sut.Extract(data.ToArray()).IsValid.Should().BeFalse();
    }
}
