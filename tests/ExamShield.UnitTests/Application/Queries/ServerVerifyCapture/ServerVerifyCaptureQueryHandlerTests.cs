using ExamShield.Application.Queries.ServerVerifyCapture;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.Services;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.ServerVerifyCapture;

public sealed class ServerVerifyCaptureQueryHandlerTests
{
    private readonly ICaptureRepository _captures = Substitute.For<ICaptureRepository>();
    private readonly IImageStorage _storage = Substitute.For<IImageStorage>();
    private readonly HashVerificationService _hashService = new();
    private readonly IDeviceRepository _devices = Substitute.For<IDeviceRepository>();
    private readonly ISignatureVerificationService _sigService = Substitute.For<ISignatureVerificationService>();
    private readonly IAuditLogRepository _auditLog = Substitute.For<IAuditLogRepository>();
    private readonly IAlertService _alertService = Substitute.For<IAlertService>();
    private readonly IWatermarkService _watermark = Substitute.For<IWatermarkService>();
    private readonly ServerVerifyCaptureQueryHandler _sut;

    public ServerVerifyCaptureQueryHandlerTests() =>
        _sut = new(_captures, _storage, _hashService, _devices, _sigService, _auditLog, _alertService, _watermark);

    private static Capture MakeUploadedCapture(byte[] imageBytes)
    {
        var hash = Hash.FromBytes(System.Security.Cryptography.SHA256.HashData(imageBytes));
        var sig = new Signature(new byte[64]);
        var cap = Capture.Create(ExamId.New(), StudentId.New(), DeviceId.New(), new PageNumber(1), hash, sig);
        cap.RecordUpload("stored/key.jpg");
        return cap;
    }

    [Fact]
    public async Task Handle_CaptureNotFound_ThrowsCaptureNotFoundException()
    {
        _captures.GetByIdAsync(Arg.Any<CaptureId>(), default).Returns((Capture?)null);

        await FluentActions.Invoking(() => _sut.Handle(new(Guid.NewGuid()), default))
            .Should().ThrowAsync<CaptureNotFoundException>();
    }

    [Fact]
    public async Task Handle_NotUploaded_ThrowsCaptureNotUploadedException()
    {
        var hash = Hash.FromBytes(new byte[32]);
        var cap = Capture.Create(ExamId.New(), StudentId.New(), DeviceId.New(), new PageNumber(1), hash, new Signature(new byte[64]));
        _captures.GetByIdAsync(cap.Id, default).Returns(cap);

        await FluentActions.Invoking(() => _sut.Handle(new(cap.Id.Value), default))
            .Should().ThrowAsync<CaptureNotUploadedException>();
    }

    [Fact]
    public async Task Handle_ValidHashAndSignature_ReturnsIsValidTrue()
    {
        var imageBytes = new byte[] { 10, 20, 30 };
        var cap = MakeUploadedCapture(imageBytes);

        var storedBytes = new byte[imageBytes.Length + 8];
        imageBytes.CopyTo(storedBytes, 0);

        _captures.GetByIdAsync(cap.Id, default).Returns(cap);
        _storage.RetrieveAsync("stored/key.jpg", default).Returns(storedBytes);
        _watermark.Extract(storedBytes).Returns(WatermarkExtractionResult.Success(null!, imageBytes.Length));

        var device = Device.Register("Phone", new PublicKey(new byte[32]));
        _devices.GetByIdAsync(cap.DeviceId, default).Returns(device);
        _sigService.Verify(Arg.Any<Hash>(), Arg.Any<Signature>(), Arg.Any<PublicKey>()).Returns(true);

        var result = await _sut.Handle(new(cap.Id.Value), default);

        result.IsValid.Should().BeTrue();
        result.HashValid.Should().BeTrue();
        result.SignatureValid.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_InvalidWatermark_HashValidFalse_SendsAlert()
    {
        var imageBytes = new byte[] { 1, 2, 3 };
        var cap = MakeUploadedCapture(imageBytes);

        _captures.GetByIdAsync(cap.Id, default).Returns(cap);
        _storage.RetrieveAsync("stored/key.jpg", default).Returns(new byte[] { 9, 9 });
        _watermark.Extract(Arg.Any<byte[]>()).Returns(WatermarkExtractionResult.Failure());
        _devices.GetByIdAsync(cap.DeviceId, default).Returns((Device?)null);

        var result = await _sut.Handle(new(cap.Id.Value), default);

        result.IsValid.Should().BeFalse();
        result.HashValid.Should().BeFalse();
        await _alertService.Received(1).SendAsync(Arg.Any<AlertType>(), Arg.Any<string>(), default);
    }

    [Fact]
    public async Task Handle_ValidVerification_AppendsHashVerifiedAuditLog()
    {
        var imageBytes = new byte[] { 5, 6, 7 };
        var cap = MakeUploadedCapture(imageBytes);
        var storedBytes = new byte[imageBytes.Length + 8];
        imageBytes.CopyTo(storedBytes, 0);

        _captures.GetByIdAsync(cap.Id, default).Returns(cap);
        _storage.RetrieveAsync("stored/key.jpg", default).Returns(storedBytes);
        _watermark.Extract(storedBytes).Returns(WatermarkExtractionResult.Success(null!, imageBytes.Length));

        var device = Device.Register("Phone", new PublicKey(new byte[32]));
        _devices.GetByIdAsync(cap.DeviceId, default).Returns(device);
        _sigService.Verify(Arg.Any<Hash>(), Arg.Any<Signature>(), Arg.Any<PublicKey>()).Returns(true);

        await _sut.Handle(new(cap.Id.Value), default);

        await _auditLog.Received(1).AppendAsync(
            Arg.Is<AuditLog>(a => a.Action == AuditAction.HashVerified), default);
    }

    [Fact]
    public async Task Handle_TamperedHash_AppendsAuditLogWithTamperingAction()
    {
        var imageBytes = new byte[] { 1, 2, 3 };
        var cap = MakeUploadedCapture(imageBytes);
        // stored bytes don't match — different content
        var storedBytes = new byte[] { 99, 99, 99, 0, 0, 0, 0, 0 };

        _captures.GetByIdAsync(cap.Id, default).Returns(cap);
        _storage.RetrieveAsync("stored/key.jpg", default).Returns(storedBytes);
        _watermark.Extract(storedBytes).Returns(WatermarkExtractionResult.Success(null!, 3));
        _devices.GetByIdAsync(cap.DeviceId, default).Returns((Device?)null);

        await _sut.Handle(new(cap.Id.Value), default);

        await _auditLog.Received(1).AppendAsync(
            Arg.Is<AuditLog>(a => a.Action == AuditAction.TamperingDetected), default);
    }
}
