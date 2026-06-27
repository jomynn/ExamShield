using ExamShield.Application.Commands.TriggerOcr;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands;

public sealed class WatermarkTamperingDetectionTests
{
    private readonly ICaptureRepository       _captures      = Substitute.For<ICaptureRepository>();
    private readonly IImageStorage            _imageStorage  = Substitute.For<IImageStorage>();
    private readonly IWatermarkService        _watermark     = Substitute.For<IWatermarkService>();
    private readonly IOcrService              _ocrService    = Substitute.For<IOcrService>();
    private readonly IOcrResultRepository     _ocrResults    = Substitute.For<IOcrResultRepository>();
    private readonly IManualReviewRepository  _manualReviews = Substitute.For<IManualReviewRepository>();
    private readonly IAuditLogRepository      _auditLog      = Substitute.For<IAuditLogRepository>();
    private readonly ISystemSettingsRepository _settings     = Substitute.For<ISystemSettingsRepository>();
    private readonly ISecurityEventRepository _secEvents     = Substitute.For<ISecurityEventRepository>();
    private readonly TriggerOcrCommandHandler _sut;

    public WatermarkTamperingDetectionTests()
    {
        _settings.GetAsync(Arg.Any<CancellationToken>())
                 .Returns(SystemSettings.CreateDefault());
        _sut = new TriggerOcrCommandHandler(
            _captures, _imageStorage, _watermark, _ocrService,
            _ocrResults, _manualReviews, _auditLog, _settings, _secEvents);
    }

    private static Capture MakeUploadedCapture()
    {
        var capture = Capture.Create(
            new ExamId(Guid.NewGuid()), new StudentId(Guid.NewGuid()), DeviceId.New(),
            new PageNumber(1),
            Hash.FromHex("aabbccdd" + new string('0', 56)),
            new Signature(new byte[64]));
        capture.RecordUpload("storage/key.jpg");
        return capture;
    }

    [Fact]
    public async Task Handle_WhenWatermarkInvalid_FlagsCaptureTampered()
    {
        var capture = MakeUploadedCapture();
        _captures.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);
        _imageStorage.RetrieveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                     .Returns(new byte[100]);
        _watermark.Extract(Arg.Any<byte[]>()).Returns(WatermarkExtractionResult.Failure());

        await _sut.Invoking(s => s.Handle(new TriggerOcrCommand(capture.Id.Value), default))
                  .Should().ThrowAsync<Exception>();

        capture.Status.Should().Be(CaptureStatus.Tampered);
    }

    [Fact]
    public async Task Handle_WhenWatermarkInvalid_EmitsSecurityEvent()
    {
        var capture = MakeUploadedCapture();
        _captures.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);
        _imageStorage.RetrieveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                     .Returns(new byte[100]);
        _watermark.Extract(Arg.Any<byte[]>()).Returns(WatermarkExtractionResult.Failure());

        try { await _sut.Handle(new TriggerOcrCommand(capture.Id.Value), default); } catch { }

        await _secEvents.Received(1).AddAsync(
            Arg.Is<SecurityEvent>(e =>
                e.EventType == SecurityEventType.WatermarkTampered &&
                e.Severity  == SecuritySeverity.Critical),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenWatermarkInvalid_AppendsAuditTamperingDetected()
    {
        var capture = MakeUploadedCapture();
        _captures.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);
        _imageStorage.RetrieveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                     .Returns(new byte[100]);
        _watermark.Extract(Arg.Any<byte[]>()).Returns(WatermarkExtractionResult.Failure());

        try { await _sut.Handle(new TriggerOcrCommand(capture.Id.Value), default); } catch { }

        await _auditLog.Received(1).AppendAsync(
            Arg.Is<AuditLog>(a => a.Action == AuditAction.TamperingDetected),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenWatermarkInvalid_DoesNotRunOcr()
    {
        var capture = MakeUploadedCapture();
        _captures.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);
        _imageStorage.RetrieveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
                     .Returns(new byte[100]);
        _watermark.Extract(Arg.Any<byte[]>()).Returns(WatermarkExtractionResult.Failure());

        try { await _sut.Handle(new TriggerOcrCommand(capture.Id.Value), default); } catch { }

        await _ocrService.DidNotReceive().ExtractAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenWatermarkValid_ProceedsWithOcr()
    {
        var capture = MakeUploadedCapture();
        _captures.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);
        var bytes = new byte[200];
        _imageStorage.RetrieveAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(bytes);
        _watermark.Extract(bytes).Returns(WatermarkExtractionResult.Success(
            new WatermarkPayload { CaptureId = capture.Id.Value, ExamId = capture.ExamId.Value }, 100));
        _ocrService.ExtractAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
                   .Returns(new OcrExtractionResult([]));

        await _sut.Handle(new TriggerOcrCommand(capture.Id.Value), default);

        await _ocrService.Received(1).ExtractAsync(Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
        await _secEvents.DidNotReceive().AddAsync(Arg.Any<SecurityEvent>(), Arg.Any<CancellationToken>());
    }
}
