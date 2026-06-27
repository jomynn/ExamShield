using ExamShield.Application.Commands.TriggerOcr;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands;

public sealed class TriggerOcrConfidenceThresholdTests
{
    private readonly ICaptureRepository   _captures      = Substitute.For<ICaptureRepository>();
    private readonly IImageStorage        _storage       = Substitute.For<IImageStorage>();
    private readonly IWatermarkService    _watermark     = Substitute.For<IWatermarkService>();
    private readonly IOcrService          _ocrService    = Substitute.For<IOcrService>();
    private readonly IOcrResultRepository _ocrResults    = Substitute.For<IOcrResultRepository>();
    private readonly IManualReviewRepository _reviews    = Substitute.For<IManualReviewRepository>();
    private readonly IAuditLogRepository  _audit         = Substitute.For<IAuditLogRepository>();
    private readonly ISystemSettingsRepository _settings  = Substitute.For<ISystemSettingsRepository>();
    private readonly ISecurityEventRepository  _secEvents = Substitute.For<ISecurityEventRepository>();

    private TriggerOcrCommandHandler BuildSut() => new(
        _captures, _storage, _watermark, _ocrService, _ocrResults, _reviews, _audit, _settings, _secEvents);

    private void SetupCapture(out CaptureId captureId)
    {
        var capture = Capture.Create(
            ExamId.New(), StudentId.New(), DeviceId.New(),
            new PageNumber(1), Hash.FromBytes(new byte[32]), new Signature(new byte[64]));
        capture.RecordUpload("storage/key");
        captureId = capture.Id;
        _captures.GetByIdAsync(captureId, Arg.Any<CancellationToken>()).Returns(capture);
        var imageBytes = new byte[200];
        _storage.RetrieveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(imageBytes);
        _watermark.Extract(imageBytes)
            .Returns(WatermarkExtractionResult.Success(
                new WatermarkPayload { CaptureId = captureId.Value, ExamId = Guid.NewGuid() }, 100));
    }

    [Fact]
    public async Task Handle_WhenConfidenceAboveThreshold_DoesNotRouteToManualReview()
    {
        SetupCapture(out var captureId);

        // System threshold set to 0.7; OCR returns confidence 0.9 → above threshold
        _settings.GetAsync(Arg.Any<CancellationToken>())
            .Returns(SystemSettings.CreateDefault());   // default threshold is 0.85

        _ocrService.ExtractAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new OcrExtractionResult([
                new ExtractedAnswer(1, "A", new OcrConfidence(0.95))
            ]));

        var result = await BuildSut().Handle(new TriggerOcrCommand(captureId.Value), default);

        result.RequiresManualReview.Should().BeFalse();
        await _reviews.DidNotReceive().AddAsync(Arg.Any<ManualReview>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenConfidenceBelowSystemThreshold_RoutesToManualReview()
    {
        SetupCapture(out var captureId);

        // System threshold 0.85; OCR returns 0.70 → below → manual review
        _settings.GetAsync(Arg.Any<CancellationToken>())
            .Returns(SystemSettings.CreateDefault()); // default is 0.85

        _ocrService.ExtractAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new OcrExtractionResult([
                new ExtractedAnswer(1, "A", new OcrConfidence(0.70))
            ]));

        var result = await BuildSut().Handle(new TriggerOcrCommand(captureId.Value), default);

        result.RequiresManualReview.Should().BeTrue();
        await _reviews.Received(1).AddAsync(Arg.Any<ManualReview>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCustomThresholdLower_HigherConfidencePassesThrough()
    {
        SetupCapture(out var captureId);

        // Admin lowered threshold to 0.6; OCR returns 0.75 → above → no review
        var settings = SystemSettings.CreateDefault();
        settings.Update(0.6, true, "High", 60, 7);
        _settings.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);

        _ocrService.ExtractAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new OcrExtractionResult([
                new ExtractedAnswer(1, "A", new OcrConfidence(0.75))
            ]));

        var result = await BuildSut().Handle(new TriggerOcrCommand(captureId.Value), default);

        result.RequiresManualReview.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenCustomThresholdHigher_LowerConfidenceTriggersReview()
    {
        SetupCapture(out var captureId);

        // Admin raised threshold to 0.95; OCR returns 0.90 → below → manual review
        var settings = SystemSettings.CreateDefault();
        settings.Update(0.95, true, "High", 60, 7);
        _settings.GetAsync(Arg.Any<CancellationToken>()).Returns(settings);

        _ocrService.ExtractAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(new OcrExtractionResult([
                new ExtractedAnswer(1, "A", new OcrConfidence(0.90))
            ]));

        var result = await BuildSut().Handle(new TriggerOcrCommand(captureId.Value), default);

        result.RequiresManualReview.Should().BeTrue();
    }
}
