using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.TriggerOcr;

public sealed class TriggerOcrCommandHandler : IRequestHandler<TriggerOcrCommand, TriggerOcrResult>
{
    private readonly ICaptureRepository _captures;
    private readonly IImageStorage _imageStorage;
    private readonly IWatermarkService _watermark;
    private readonly IOcrService _ocrService;
    private readonly IOcrResultRepository _ocrResults;
    private readonly IManualReviewRepository _manualReviews;
    private readonly IAuditLogRepository _auditLog;
    private readonly ISystemSettingsRepository _systemSettings;

    public TriggerOcrCommandHandler(
        ICaptureRepository captures, IImageStorage imageStorage,
        IWatermarkService watermark, IOcrService ocrService,
        IOcrResultRepository ocrResults, IManualReviewRepository manualReviews,
        IAuditLogRepository auditLog, ISystemSettingsRepository systemSettings)
    {
        _captures = captures;
        _imageStorage = imageStorage;
        _watermark = watermark;
        _ocrService = ocrService;
        _ocrResults = ocrResults;
        _manualReviews = manualReviews;
        _auditLog = auditLog;
        _systemSettings = systemSettings;
    }

    public async Task<TriggerOcrResult> Handle(TriggerOcrCommand command, CancellationToken ct)
    {
        var capture = await _captures.GetByIdAsync(new CaptureId(command.CaptureId), ct)
            ?? throw new CaptureNotFoundException(command.CaptureId);

        if (capture.StorageKey is null)
            throw new CaptureNotUploadedException(command.CaptureId);

        var settings      = await _systemSettings.GetAsync(ct);
        var storedBytes   = await _imageStorage.RetrieveAsync(capture.StorageKey, ct);
        var wm            = _watermark.Extract(storedBytes);
        var imageBytes    = wm.IsValid ? storedBytes[..wm.OriginalImageLength] : storedBytes;

        var extraction = await _ocrService.ExtractAsync(imageBytes, ct);
        var ocrResult  = OcrResult.Create(capture.Id, extraction.Answers, settings.OcrConfidenceThreshold);

        await _ocrResults.AddAsync(ocrResult, ct);

        if (ocrResult.RequiresManualReview)
        {
            var review = ManualReview.CreateFor(ocrResult);
            await _manualReviews.AddAsync(review, ct);
            await _auditLog.AppendAsync(
                AuditLog.Record(AuditAction.ManualReviewStarted, captureId: capture.Id), ct);
        }

        await _auditLog.AppendAsync(
            AuditLog.Record(AuditAction.OCRCompleted, captureId: capture.Id), ct);

        return new TriggerOcrResult(ocrResult.Id.Value, ocrResult.Status, ocrResult.RequiresManualReview);
    }
}
