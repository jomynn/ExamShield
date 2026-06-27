using ExamShield.Application.Commands.TriggerOcr;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands;

public sealed class TriggerOcrCommandHandlerTests
{
    private readonly ICaptureRepository _captures = Substitute.For<ICaptureRepository>();
    private readonly IImageStorage _imageStorage = Substitute.For<IImageStorage>();
    private readonly IWatermarkService _watermark = Substitute.For<IWatermarkService>();
    private readonly IOcrService _ocrService = Substitute.For<IOcrService>();
    private readonly IOcrResultRepository _ocrResults = Substitute.For<IOcrResultRepository>();
    private readonly IManualReviewRepository _manualReviews = Substitute.For<IManualReviewRepository>();
    private readonly IAuditLogRepository _auditLog = Substitute.For<IAuditLogRepository>();
    private readonly ISystemSettingsRepository _settings = Substitute.For<ISystemSettingsRepository>();
    private readonly TriggerOcrCommandHandler _sut;

    private static readonly byte[] ImageBytes = "exam-image"u8.ToArray();

    public TriggerOcrCommandHandlerTests()
    {
        // Watermark strips cleanly — OriginalImageLength equals full array so slicing is identity.
        _watermark.Extract(Arg.Any<byte[]>())
            .Returns(WatermarkExtractionResult.Success(new WatermarkPayload(), ImageBytes.Length));

        _settings.GetAsync(Arg.Any<CancellationToken>()).Returns(SystemSettings.CreateDefault());

        _sut = new TriggerOcrCommandHandler(
            _captures, _imageStorage, _watermark, _ocrService, _ocrResults, _manualReviews, _auditLog, _settings);
    }

    private static Capture UploadedCapture()
    {
        var capture = Capture.Create(ExamId.New(), StudentId.New(), DeviceId.New(),
            new PageNumber(1), Hash.FromHex(new string('a', 64)), new Signature(new byte[64]));
        capture.RecordUpload("storage/key/image.jpg");
        return capture;
    }

    private static OcrExtractionResult HighConfidenceExtraction() => new(
    [
        new ExtractedAnswer(1, "A", new OcrConfidence(0.95)),
        new ExtractedAnswer(2, "B", new OcrConfidence(0.90))
    ]);

    private static OcrExtractionResult LowConfidenceExtraction() => new(
    [
        new ExtractedAnswer(1, "A", new OcrConfidence(0.95)),
        new ExtractedAnswer(2, "C", new OcrConfidence(0.55))
    ]);

    [Fact]
    public async Task Handle_WithUploadedCapture_CallsOcrService()
    {
        var capture = UploadedCapture();
        _captures.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);
        _imageStorage.RetrieveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ImageBytes);
        _ocrService.ExtractAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(HighConfidenceExtraction());

        await _sut.Handle(new TriggerOcrCommand(capture.Id.Value), default);

        // Slicing creates a new array; verify by content rather than reference.
        await _ocrService.Received(1).ExtractAsync(
            Arg.Is<byte[]>(b => b.SequenceEqual(ImageBytes)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithUploadedCapture_StoresOcrResult()
    {
        var capture = UploadedCapture();
        _captures.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);
        _imageStorage.RetrieveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ImageBytes);
        _ocrService.ExtractAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(HighConfidenceExtraction());

        await _sut.Handle(new TriggerOcrCommand(capture.Id.Value), default);

        await _ocrResults.Received(1).AddAsync(Arg.Any<OcrResult>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenHighConfidence_DoesNotCreateManualReview()
    {
        var capture = UploadedCapture();
        _captures.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);
        _imageStorage.RetrieveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ImageBytes);
        _ocrService.ExtractAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(HighConfidenceExtraction());

        await _sut.Handle(new TriggerOcrCommand(capture.Id.Value), default);

        await _manualReviews.DidNotReceive().AddAsync(Arg.Any<ManualReview>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenLowConfidence_CreatesManualReview()
    {
        var capture = UploadedCapture();
        _captures.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);
        _imageStorage.RetrieveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ImageBytes);
        _ocrService.ExtractAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(LowConfidenceExtraction());

        await _sut.Handle(new TriggerOcrCommand(capture.Id.Value), default);

        await _manualReviews.Received(1).AddAsync(
            Arg.Is<ManualReview>(r => r.CaptureId == capture.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCaptureNotFound_ThrowsCaptureNotFoundException()
    {
        _captures.GetByIdAsync(Arg.Any<CaptureId>(), Arg.Any<CancellationToken>())
            .Returns((Capture?)null);

        await Assert.ThrowsAsync<CaptureNotFoundException>(() =>
            _sut.Handle(new TriggerOcrCommand(Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Handle_WhenCaptureNotUploaded_ThrowsCaptureNotUploadedException()
    {
        var capture = Capture.Create(ExamId.New(), StudentId.New(), DeviceId.New(),
            new PageNumber(1), Hash.FromHex(new string('a', 64)), new Signature(new byte[64]));
        _captures.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);

        await Assert.ThrowsAsync<CaptureNotUploadedException>(() =>
            _sut.Handle(new TriggerOcrCommand(capture.Id.Value), default));
    }

    [Fact]
    public async Task Handle_AfterOcr_AppendsOcrCompletedAuditEntry()
    {
        var capture = UploadedCapture();
        _captures.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);
        _imageStorage.RetrieveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ImageBytes);
        _ocrService.ExtractAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(HighConfidenceExtraction());

        await _sut.Handle(new TriggerOcrCommand(capture.Id.Value), default);

        await _auditLog.Received(1).AppendAsync(
            Arg.Is<AuditLog>(e => e.Action == AuditAction.OCRCompleted && e.CaptureId == capture.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_StripsWatermarkBeforeCallingOcrService()
    {
        // Stored bytes = original + trailing watermark byte; OriginalImageLength = original length.
        var storedBytes = ImageBytes.Append((byte)0xFF).ToArray();
        _watermark.Extract(storedBytes)
            .Returns(WatermarkExtractionResult.Success(new WatermarkPayload(), ImageBytes.Length));

        var capture = UploadedCapture();
        _captures.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);
        _imageStorage.RetrieveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(storedBytes);
        _ocrService.ExtractAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(HighConfidenceExtraction());

        await _sut.Handle(new TriggerOcrCommand(capture.Id.Value), default);

        await _ocrService.Received(1).ExtractAsync(
            Arg.Is<byte[]>(b => b.SequenceEqual(ImageBytes)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenWatermarkExtractionFails_PassesFullBytesToOcr()
    {
        _watermark.Extract(Arg.Any<byte[]>()).Returns(WatermarkExtractionResult.Failure());

        var capture = UploadedCapture();
        _captures.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);
        _imageStorage.RetrieveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(ImageBytes);
        _ocrService.ExtractAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns(HighConfidenceExtraction());

        await _sut.Handle(new TriggerOcrCommand(capture.Id.Value), default);

        await _ocrService.Received(1).ExtractAsync(ImageBytes, Arg.Any<CancellationToken>());
    }
}
