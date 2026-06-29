using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.Services;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Queries.PublicVerifyCapture;

public sealed class PublicVerifyCaptureQueryHandler(
    ICaptureRepository captures,
    IDeviceRepository devices,
    IImageStorage imageStorage,
    IImageEncryptionService encryption,
    HashVerificationService hashService,
    ISignatureVerificationService sigService,
    IWatermarkService watermarkService)
    : IRequestHandler<PublicVerifyCaptureQuery, PublicVerifyResult>
{
    public async Task<PublicVerifyResult> Handle(PublicVerifyCaptureQuery request, CancellationToken ct)
    {
        var capture = request.CaptureId.HasValue
            ? await captures.GetByIdAsync(new CaptureId(request.CaptureId.Value), ct)
            : await captures.FindByHashAsync(Hash.FromHex(request.HashHex!), ct);

        if (capture is null)
            throw new CaptureNotFoundException(request.CaptureId ?? Guid.Empty);

        if (capture.StorageKey is null)
            return new PublicVerifyResult(capture.Id.Value, false, false, false, false, capture.CapturedAt);

        var rawBytes = await imageStorage.RetrieveAsync(capture.StorageKey, ct);
        var storedBytes = capture.EncryptedDek is not null
            ? encryption.Decrypt(rawBytes, capture.EncryptedDek)
            : rawBytes;

        var extraction = watermarkService.Extract(storedBytes);

        var hashValid = extraction.IsValid &&
            hashService.ComputeHash(storedBytes[..extraction.OriginalImageLength]) == capture.ExpectedHash;

        var device = await devices.GetByIdAsync(capture.DeviceId, ct);
        var sigValid = device is not null &&
            sigService.Verify(capture.ExpectedHash, capture.Signature, device.PublicKey);

        return new PublicVerifyResult(
            capture.Id.Value,
            hashValid && sigValid,
            hashValid,
            sigValid,
            true,
            capture.CapturedAt
        );
    }
}
