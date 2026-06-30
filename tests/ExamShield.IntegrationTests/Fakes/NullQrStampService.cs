using ExamShield.Domain.Interfaces;

namespace ExamShield.IntegrationTests.Fakes;

/// <summary>
/// No-op QR stamp for integration tests. Real images aren't present, so skip the overlay.
/// </summary>
public sealed class NullQrStampService : IQrStampService
{
    public byte[] Stamp(byte[] imageBytes, Guid captureId, Guid examId, string imageHashHex)
        => imageBytes;
}
