using ExamShield.Infrastructure.Watermark;
using FluentAssertions;
using SkiaSharp;
using Xunit;

namespace ExamShield.UnitTests.Infrastructure.Watermark;

public sealed class QrStampServiceTests
{
    private readonly QrStampService _sut = new();

    // Creates an opaque JPEG large enough (640×480) for the QR guard to pass (must be ≥ 480 pixels wide/tall)
    private static byte[] CreateJpeg(int width = 640, int height = 480)
    {
        var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Opaque);
        using var bmp    = new SKBitmap(info);
        using var canvas = new SKCanvas(bmp);
        canvas.Clear(new SKColor(200, 200, 200, 255));
        using var img  = SKImage.FromBitmap(bmp);
        using var data = img.Encode(SKEncodedImageFormat.Jpeg, 90);
        return data.ToArray();
    }

    [Fact]
    public void Stamp_LargeImage_ReturnsNonEmptyBytes()
    {
        var input = CreateJpeg();
        var result = _sut.Stamp(input, Guid.NewGuid(), Guid.NewGuid(), "abcdef1234567890");
        result.Should().NotBeEmpty();
    }

    [Fact]
    public void Stamp_LargeImage_IsDecodable()
    {
        var input  = CreateJpeg();
        var result = _sut.Stamp(input, Guid.NewGuid(), Guid.NewGuid(), "abcdef1234567890");
        using var bitmap = SKBitmap.Decode(result);
        bitmap.Should().NotBeNull();
    }

    [Fact]
    public void Stamp_LargeImage_PreservesOriginalDimensions()
    {
        var input  = CreateJpeg(800, 600);
        var result = _sut.Stamp(input, Guid.NewGuid(), Guid.NewGuid(), "deadbeef01234567");
        using var bitmap = SKBitmap.Decode(result);
        bitmap!.Width.Should().Be(800);
        bitmap.Height.Should().Be(600);
    }

    [Fact]
    public void Stamp_SmallImage_ReturnsBytesUnchanged()
    {
        // Image smaller than QrSize*4 (480 pixels) — stamp is skipped
        var input  = CreateJpeg(100, 100);
        var result = _sut.Stamp(input, Guid.NewGuid(), Guid.NewGuid(), "hash");
        result.Should().BeEquivalentTo(input);
    }

    [Fact]
    public void Stamp_InvalidBytes_ReturnsBytesUnchanged()
    {
        var garbage = new byte[] { 0x00, 0x01, 0xFF, 0xAB };
        var result  = _sut.Stamp(garbage, Guid.NewGuid(), Guid.NewGuid(), "hash");
        result.Should().BeEquivalentTo(garbage);
    }
}
