using ExamShield.Application.Queries.PublicVerifyCapture;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.Services;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.PublicVerifyCapture;

public sealed class PublicVerifyCaptureQueryHandlerTests
{
    private readonly ICaptureRepository _captures = Substitute.For<ICaptureRepository>();
    private readonly IDeviceRepository _devices = Substitute.For<IDeviceRepository>();
    private readonly IImageStorage _storage = Substitute.For<IImageStorage>();
    private readonly IImageEncryptionService _encryption = Substitute.For<IImageEncryptionService>();
    private readonly HashVerificationService _hashService = new();
    private readonly ISignatureVerificationService _sigService = Substitute.For<ISignatureVerificationService>();
    private readonly IWatermarkService _watermark = Substitute.For<IWatermarkService>();
    private readonly PublicVerifyCaptureQueryHandler _sut;

    public PublicVerifyCaptureQueryHandlerTests() =>
        _sut = new(_captures, _devices, _storage, _encryption, _hashService, _sigService, _watermark);

    private static Capture MakeCapture(byte[] imageBytes)
    {
        var hash = Hash.FromBytes(System.Security.Cryptography.SHA256.HashData(imageBytes));
        var sig = new Signature(new byte[64]);
        return Capture.Create(ExamId.New(), StudentId.New(), DeviceId.New(), new PageNumber(1), hash, sig);
    }

    [Fact]
    public async Task Handle_CaptureNotFound_ThrowsCaptureNotFoundException()
    {
        _captures.GetByIdAsync(Arg.Any<CaptureId>(), default).Returns((Capture?)null);

        await FluentActions.Invoking(() => _sut.Handle(new(CaptureId: Guid.NewGuid()), default))
            .Should().ThrowAsync<CaptureNotFoundException>();
    }

    [Fact]
    public async Task Handle_NotUploaded_ReturnsIsValidFalse()
    {
        var capture = MakeCapture(new byte[] { 1, 2, 3 });
        // No storage key — not uploaded
        _captures.GetByIdAsync(capture.Id, default).Returns(capture);

        var result = await _sut.Handle(new(CaptureId: capture.Id.Value), default);

        result.IsValid.Should().BeFalse();
        result.IsUploaded.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ValidHashAndSignature_ReturnsIsValidTrue()
    {
        var imageBytes = new byte[] { 10, 20, 30, 40 };
        var capture = MakeCapture(imageBytes);
        capture.RecordUpload("key/capture.jpg");

        _captures.GetByIdAsync(capture.Id, default).Returns(capture);

        var storedBytes = new byte[imageBytes.Length + 4]; // watermark appended
        imageBytes.CopyTo(storedBytes, 0);
        _storage.RetrieveAsync("key/capture.jpg", default).Returns(storedBytes);

        var extraction = WatermarkExtractionResult.Success(null!, imageBytes.Length);
        _watermark.Extract(storedBytes).Returns(extraction);

        var device = Device.Register("Phone", new PublicKey(new byte[32]));
        _devices.GetByIdAsync(capture.DeviceId, default).Returns(device);
        _sigService.Verify(Arg.Any<Hash>(), Arg.Any<Signature>(), Arg.Any<PublicKey>()).Returns(true);

        var result = await _sut.Handle(new(CaptureId: capture.Id.Value), default);

        result.HashValid.Should().BeTrue();
        result.SignatureValid.Should().BeTrue();
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_InvalidWatermark_ReturnsHashValidFalse()
    {
        var capture = MakeCapture(new byte[] { 1, 2, 3 });
        capture.RecordUpload("key");
        _captures.GetByIdAsync(capture.Id, default).Returns(capture);
        _storage.RetrieveAsync("key", default).Returns(new byte[] { 9, 9, 9 });

        var failedExtraction = WatermarkExtractionResult.Failure();
        _watermark.Extract(Arg.Any<byte[]>()).Returns(failedExtraction);

        _devices.GetByIdAsync(capture.DeviceId, default).Returns((Device?)null);

        var result = await _sut.Handle(new(CaptureId: capture.Id.Value), default);

        result.HashValid.Should().BeFalse();
        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_DeviceNotFound_SignatureValidFalse()
    {
        var imageBytes = new byte[] { 5, 6, 7 };
        var capture = MakeCapture(imageBytes);
        capture.RecordUpload("key2");
        _captures.GetByIdAsync(capture.Id, default).Returns(capture);

        var storedBytes = new byte[imageBytes.Length + 4];
        imageBytes.CopyTo(storedBytes, 0);
        _storage.RetrieveAsync("key2", default).Returns(storedBytes);

        _watermark.Extract(storedBytes).Returns(WatermarkExtractionResult.Success(null!, imageBytes.Length));
        _devices.GetByIdAsync(capture.DeviceId, default).Returns((Device?)null);

        var result = await _sut.Handle(new(CaptureId: capture.Id.Value), default);

        result.SignatureValid.Should().BeFalse();
    }
}
