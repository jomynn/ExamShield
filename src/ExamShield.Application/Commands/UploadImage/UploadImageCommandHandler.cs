using System.Security.Cryptography;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.Services;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.UploadImage;

public sealed class UploadImageCommandHandler : IRequestHandler<UploadImageCommand, UploadImageResult>
{
    private readonly ICaptureRepository _repository;
    private readonly HashVerificationService _hashService;
    private readonly IImageStorage _imageStorage;
    private readonly IAuditLogRepository _auditLog;
    private readonly IWatermarkService _watermarkService;
    private readonly ISecurityEventRepository _securityEvents;
    private readonly IImageEncryptionService _encryption;
    private readonly IQrStampService _qrStamp;

    public UploadImageCommandHandler(
        ICaptureRepository repository,
        HashVerificationService hashService,
        IImageStorage imageStorage,
        IAuditLogRepository auditLog,
        IWatermarkService watermarkService,
        ISecurityEventRepository securityEvents,
        IImageEncryptionService encryption,
        IQrStampService qrStamp)
    {
        _repository = repository;
        _hashService = hashService;
        _imageStorage = imageStorage;
        _auditLog = auditLog;
        _watermarkService = watermarkService;
        _securityEvents = securityEvents;
        _encryption = encryption;
        _qrStamp = qrStamp;
    }

    public async Task<UploadImageResult> Handle(UploadImageCommand command, CancellationToken ct)
    {
        if (command.ImageBytes.Length == 0)
            throw new ArgumentException("Image bytes cannot be empty.", nameof(command.ImageBytes));

        var capture = await _repository.GetByIdAsync(new CaptureId(command.CaptureId), ct)
            ?? throw new CaptureNotFoundException(command.CaptureId);

        if (capture.Status != CaptureStatus.Created)
        {
            await _securityEvents.AddAsync(SecurityEvent.Create(
                SecurityEventType.DuplicateUpload,
                SecuritySeverity.Warning,
                $"Duplicate upload attempt for capture {command.CaptureId}.",
                captureId: command.CaptureId), ct);
            throw new DuplicateUploadException(command.CaptureId);
        }

        var actualHash = _hashService.ComputeHash(command.ImageBytes);
        if (actualHash != capture.ExpectedHash)
        {
            await _securityEvents.AddAsync(SecurityEvent.Create(
                SecurityEventType.HashMismatch,
                SecuritySeverity.Critical,
                $"Hash mismatch for capture {command.CaptureId}. Expected {capture.ExpectedHash.Hex}, got {actualHash.Hex}.",
                captureId: command.CaptureId), ct);
            throw new HashMismatchException(command.CaptureId, capture.ExpectedHash, actualHash);
        }

        var payload = new WatermarkPayload
        {
            ExamId = capture.ExamId.Value,
            CaptureId = capture.Id.Value,
            DeviceId = capture.DeviceId.Value,
            TimestampUtcTicks = DateTimeOffset.UtcNow.UtcTicks,
            Nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(16)),
            ImageHash = capture.ExpectedHash.Hex
        };
        var watermarkedBytes = _watermarkService.Embed(command.ImageBytes, payload);

        // Overlay a visible QR code so any examiner can scan the paper copy and
        // verify provenance on the spot. Applied after hash verification and LSB
        // watermark embedding — the stored hash always refers to the raw capture.
        var stampedBytes = _qrStamp.Stamp(
            watermarkedBytes,
            capture.Id.Value,
            capture.ExamId.Value,
            capture.ExpectedHash.Hex);

        var (ciphertext, encryptedDek) = _encryption.Encrypt(stampedBytes);
        var storageKey = await _imageStorage.StoreAsync(command.CaptureId, ciphertext, ct);

        capture.RecordUpload(storageKey, encryptedDek);

        await _repository.UpdateAsync(capture, ct);
        await _auditLog.AppendAsync(
            AuditLog.Record(AuditAction.ImageUploaded, captureId: capture.Id), ct);

        return new UploadImageResult(storageKey);
    }
}
