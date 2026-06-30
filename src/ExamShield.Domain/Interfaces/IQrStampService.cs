namespace ExamShield.Domain.Interfaces;

/// <summary>
/// Overlays a visible QR code on a captured image.
/// The QR encodes provenance data (capture ID, exam ID, hash prefix) so examiners can
/// quickly verify authenticity with any QR scanner. Applied after hash verification and
/// LSB watermark embedding, before AES-256-GCM encryption and object storage write.
/// </summary>
public interface IQrStampService
{
    byte[] Stamp(byte[] imageBytes, Guid captureId, Guid examId, string imageHashHex);
}
