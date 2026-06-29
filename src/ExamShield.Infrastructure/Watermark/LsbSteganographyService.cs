using System.Text;
using System.Text.Json;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using SkiaSharp;

namespace ExamShield.Infrastructure.Watermark;

// Embeds a JSON payload invisibly in the least-significant bit of the blue
// channel of each pixel. Re-encodes as PNG so the LSBs survive exactly.
// JPEG captures are decoded first (lossy); the lossless PNG output is what
// gets stored in object storage — human-visible quality is identical.
//
// Payload envelope written into blue-channel LSBs (MSB-first per byte):
//   [4 bytes little-endian: payloadLen][payloadLen bytes: UTF-8 JSON]
//
// Capacity: width × height bits → a 1280×960 image can carry ~153 KB —
// far more than any watermark payload needs.
public sealed class LsbSteganographyService : IWatermarkService
{
    public byte[] Embed(byte[] imageBytes, WatermarkPayload payload)
    {
        var json     = JsonSerializer.SerializeToUtf8Bytes(payload);
        var lenBytes = BitConverter.GetBytes(json.Length);
        var bits     = BuildBitStream(lenBytes, json);

        using var bmp = SKBitmap.Decode(imageBytes)
            ?? throw new InvalidOperationException("Cannot decode image for watermark embedding.");

        if (bits.Count > bmp.Width * bmp.Height)
            throw new InvalidOperationException(
                $"Image too small ({bmp.Width}×{bmp.Height} = {bmp.Width * bmp.Height} pixels) " +
                $"for watermark payload ({bits.Count} bits required).");

        var idx = 0;
        for (var y = 0; y < bmp.Height && idx < bits.Count; y++)
        {
            for (var x = 0; x < bmp.Width && idx < bits.Count; x++)
            {
                var px = bmp.GetPixel(x, y);
                var newBlue = (byte)((px.Blue & 0xFE) | bits[idx++]);
                bmp.SetPixel(x, y, new SKColor(px.Red, px.Green, newBlue, px.Alpha));
            }
        }

        using var image = SKImage.FromBitmap(bmp);
        using var data  = image.Encode(SKEncodedImageFormat.Png, 100);
        return data.ToArray();
    }

    public WatermarkExtractionResult Extract(byte[] imageBytes)
    {
        try
        {
            using var bmp = SKBitmap.Decode(imageBytes);
            if (bmp is null) return WatermarkExtractionResult.Failure();

            var totalBits = bmp.Width * bmp.Height;
            var bits      = ExtractBitStream(bmp, totalBits);

            var payloadLen = BitConverter.ToInt32(BitsToBytes(bits, 0, 32));
            if (payloadLen <= 0 || payloadLen > (totalBits / 8) - 4)
                return WatermarkExtractionResult.Failure();

            var payloadBytes = BitsToBytes(bits, 32, payloadLen * 8);
            var result = JsonSerializer.Deserialize<WatermarkPayload>(
                Encoding.UTF8.GetString(payloadBytes));

            return result is null
                ? WatermarkExtractionResult.Failure()
                : WatermarkExtractionResult.Success(result, originalImageLength: imageBytes.Length);
        }
        catch
        {
            return WatermarkExtractionResult.Failure();
        }
    }

    // ─── Bit helpers ─────────────────────────────────────────────────────────

    private static List<int> BuildBitStream(params byte[][] chunks)
    {
        var bits = new List<int>();
        foreach (var chunk in chunks)
            foreach (var b in chunk)
                for (var i = 7; i >= 0; i--)
                    bits.Add((b >> i) & 1);
        return bits;
    }

    private static List<int> ExtractBitStream(SKBitmap bmp, int maxBits)
    {
        var bits = new List<int>(maxBits);
        for (var y = 0; y < bmp.Height && bits.Count < maxBits; y++)
            for (var x = 0; x < bmp.Width && bits.Count < maxBits; x++)
                bits.Add(bmp.GetPixel(x, y).Blue & 1);
        return bits;
    }

    private static byte[] BitsToBytes(List<int> bits, int offset, int count)
    {
        var bytes = new byte[count / 8];
        for (var i = 0; i < count; i += 8)
        {
            byte b = 0;
            for (var j = 0; j < 8; j++)
                b = (byte)((b << 1) | bits[offset + i + j]);
            bytes[i / 8] = b;
        }
        return bytes;
    }
}
