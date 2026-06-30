using ExamShield.Domain.Interfaces;
using QRCoder;
using SkiaSharp;

namespace ExamShield.Infrastructure.Watermark;

/// <summary>
/// Renders a QR code in the bottom-right corner of the captured image.
///
/// QR payload format (compact, scan-able by any QR reader):
///   EXAMSHIELD:{captureId}:{examId}:{hash[..12]}
///
/// The captured image is decoded → QR bitmap is drawn on top → re-encoded as JPEG (q=95)
/// so downstream processing (OCR, watermark extraction) can still work normally.
/// </summary>
public sealed class QrStampService : IQrStampService
{
    private const int QrSize     = 120;  // rendered QR square in pixels
    private const int Margin     = 10;   // distance from bottom-right edge
    private const int StripH     = 20;   // caption strip height below the QR block
    private const int JpegQuality = 95;

    public byte[] Stamp(byte[] imageBytes, Guid captureId, Guid examId, string imageHashHex)
    {
        SKBitmap? original;
        try { original = SKBitmap.Decode(imageBytes); }
        catch { return imageBytes; }  // non-decodable (corrupt/non-image) — return unchanged
        if (original is null)
            return imageBytes;
        using var _ = original;

        // Guard: image must be large enough to hold the QR without dominating it
        if (original.Width < QrSize * 4 || original.Height < (QrSize + StripH) * 4)
            return imageBytes;

        using var surface = SKSurface.Create(new SKImageInfo(original.Width, original.Height));
        var canvas = surface.Canvas;

        // Draw original image as the background
        canvas.DrawBitmap(original, 0, 0);

        // Generate QR matrix
        var payload = $"EXAMSHIELD:{captureId}:{examId}:{imageHashHex[..Math.Min(12, imageHashHex.Length)]}";
        using var generator = new QRCodeGenerator();
        using var data      = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);

        var modules    = data.ModuleMatrix;
        int moduleCount = modules.Count;
        float moduleSize = (float)QrSize / moduleCount;

        // QR block position: bottom-right corner
        float qrLeft = original.Width  - QrSize  - Margin;
        float qrTop  = original.Height - QrSize - StripH - Margin;

        // White backing rectangle (padding around QR)
        const int pad = 6;
        using var bgPaint = new SKPaint { Color = SKColors.White };
        canvas.DrawRect(
            SKRect.Create(qrLeft - pad, qrTop - pad, QrSize + pad * 2, QrSize + StripH + pad * 2),
            bgPaint);

        // Draw QR modules
        using var darkPaint  = new SKPaint { Color = SKColors.Black };
        using var lightPaint = new SKPaint { Color = SKColors.White };

        for (var row = 0; row < moduleCount; row++)
        {
            for (var col = 0; col < moduleCount; col++)
            {
                var rect = SKRect.Create(
                    qrLeft + col * moduleSize,
                    qrTop  + row * moduleSize,
                    moduleSize,
                    moduleSize);
                canvas.DrawRect(rect, modules[row][col] ? darkPaint : lightPaint);
            }
        }

        // Caption strip: "ExamShield | {captureId[..8]}"
        using var font = new SKFont(SKTypeface.Default, 10);
        using var textPaint = new SKPaint { Color = SKColors.Black };
        var caption = $"ExamShield | {captureId.ToString()[..8]}";
        canvas.DrawText(caption, qrLeft - pad + 2, qrTop + QrSize + 14, font, textPaint);

        using var snapshot = surface.Snapshot();
        using var encoded  = snapshot.Encode(SKEncodedImageFormat.Jpeg, JpegQuality);
        return encoded.ToArray();
    }
}
