using System.Security.Cryptography;
using ExamShield.Application.Commands.UploadImage;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using ExamShield.Domain.Services;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace ExamShield.UnitTests.Application.Commands;

public sealed class UploadImageCommandHandlerTests
{
    private readonly ICaptureRepository _repository = Substitute.For<ICaptureRepository>();
    private readonly IImageStorage _imageStorage = Substitute.For<IImageStorage>();
    private readonly IAuditLogRepository _auditLog = Substitute.For<IAuditLogRepository>();
    private readonly IWatermarkService _watermarkService = Substitute.For<IWatermarkService>();
    private readonly ISecurityEventRepository _securityEvents = Substitute.For<ISecurityEventRepository>();
    private readonly IImageEncryptionService _encryption = Substitute.For<IImageEncryptionService>();
    private readonly IQrStampService _qrStamp = Substitute.For<IQrStampService>();
    private readonly HashVerificationService _hashService = new();
    private readonly UploadImageCommandHandler _sut;

    private static readonly byte[] SampleImage = "answer-sheet-bytes"u8.ToArray();
    private static readonly byte[] WatermarkedImage = [..SampleImage, 0xFF, 0xFE];
    private static readonly byte[] EncryptedImage = "enc-bytes"u8.ToArray();
    private static readonly byte[] EncryptedDek = "enc-dek"u8.ToArray();
    private static readonly Hash SampleHash = Hash.FromBytes(SHA256.HashData(SampleImage));

    public UploadImageCommandHandlerTests()
    {
        _watermarkService.Embed(Arg.Any<byte[]>(), Arg.Any<WatermarkPayload>())
            .Returns(WatermarkedImage);
        _qrStamp.Stamp(Arg.Any<byte[]>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>())
            .Returns(c => c.ArgAt<byte[]>(0));  // pass-through
        _encryption.Encrypt(WatermarkedImage).Returns((EncryptedImage, EncryptedDek));
        _imageStorage.StoreAsync(Arg.Any<Guid>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns("captures/test-key");

        _sut = new UploadImageCommandHandler(
            _repository, _hashService, _imageStorage, _auditLog, _watermarkService, _securityEvents,
            _encryption, _qrStamp);
    }

    private Capture CaptureWithSampleHash()
    {
        var capture = Capture.Create(
            ExamId.New(), StudentId.New(), DeviceId.New(),
            new PageNumber(1), SampleHash, new Signature(new byte[64]));
        _repository.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);
        return capture;
    }

    [Fact]
    public async Task Handle_WithValidImageAndMatchingHash_ReturnsStorageKey()
    {
        var capture = CaptureWithSampleHash();
        var command = new UploadImageCommand(capture.Id.Value, SampleImage);

        var result = await _sut.Handle(command, CancellationToken.None);

        result.StorageKey.Should().Be("captures/test-key");
    }

    [Fact]
    public async Task Handle_WithValidImageAndMatchingHash_StoresEncryptedImage()
    {
        var capture = CaptureWithSampleHash();

        await _sut.Handle(new UploadImageCommand(capture.Id.Value, SampleImage), CancellationToken.None);

        await _imageStorage.Received(1)
            .StoreAsync(capture.Id.Value, EncryptedImage, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithValidImageAndMatchingHash_RecordsEncryptedDekOnCapture()
    {
        var capture = CaptureWithSampleHash();

        await _sut.Handle(new UploadImageCommand(capture.Id.Value, SampleImage), CancellationToken.None);

        capture.EncryptedDek.Should().Equal(EncryptedDek);
    }

    [Fact]
    public async Task Handle_WithValidImageAndMatchingHash_EmbedsWatermarkWithCorrectPayload()
    {
        var capture = CaptureWithSampleHash();

        await _sut.Handle(new UploadImageCommand(capture.Id.Value, SampleImage), CancellationToken.None);

        _watermarkService.Received(1).Embed(
            SampleImage,
            Arg.Is<WatermarkPayload>(p =>
                p.CaptureId == capture.Id.Value &&
                p.ExamId == capture.ExamId.Value &&
                p.ImageHash == capture.ExpectedHash.Hex));
    }

    [Fact]
    public async Task Handle_WithValidImageAndMatchingHash_PersistsCaptureAsUploaded()
    {
        var capture = CaptureWithSampleHash();

        await _sut.Handle(new UploadImageCommand(capture.Id.Value, SampleImage), CancellationToken.None);

        await _repository.Received(1).UpdateAsync(
            Arg.Is<Capture>(c => c.Status == CaptureStatus.Uploaded),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithValidImageAndMatchingHash_AppendsImageUploadedAuditEntry()
    {
        var capture = CaptureWithSampleHash();

        await _sut.Handle(new UploadImageCommand(capture.Id.Value, SampleImage), CancellationToken.None);

        await _auditLog.Received(1).AppendAsync(
            Arg.Is<AuditLog>(e => e.Action == AuditAction.ImageUploaded && e.CaptureId == capture.Id),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenCaptureNotFound_ThrowsCaptureNotFoundException()
    {
        _repository.GetByIdAsync(Arg.Any<CaptureId>(), Arg.Any<CancellationToken>()).Returns((Capture?)null);

        var act = () => _sut.Handle(new UploadImageCommand(Guid.NewGuid(), SampleImage), CancellationToken.None);

        await act.Should().ThrowAsync<CaptureNotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenHashMismatches_ThrowsHashMismatchException()
    {
        var capture = Capture.Create(
            ExamId.New(), StudentId.New(), DeviceId.New(),
            new PageNumber(1), Hash.FromHex(new string('b', 64)), new Signature(new byte[64]));
        _repository.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);

        var act = () => _sut.Handle(new UploadImageCommand(capture.Id.Value, SampleImage), CancellationToken.None);

        await act.Should().ThrowAsync<HashMismatchException>();
    }

    [Fact]
    public async Task Handle_WhenHashMismatches_DoesNotEmbedOrStoreImage()
    {
        var capture = Capture.Create(
            ExamId.New(), StudentId.New(), DeviceId.New(),
            new PageNumber(1), Hash.FromHex(new string('b', 64)), new Signature(new byte[64]));
        _repository.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);

        try { await _sut.Handle(new UploadImageCommand(capture.Id.Value, SampleImage), CancellationToken.None); }
        catch (HashMismatchException) { }

        _watermarkService.DidNotReceive().Embed(Arg.Any<byte[]>(), Arg.Any<WatermarkPayload>());
        _encryption.DidNotReceive().Encrypt(Arg.Any<byte[]>());
        await _imageStorage.DidNotReceive().StoreAsync(Arg.Any<Guid>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithEmptyImageBytes_ThrowsArgumentException()
    {
        var act = () => _sut.Handle(new UploadImageCommand(Guid.NewGuid(), []), CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_WhenAlreadyUploaded_ThrowsDuplicateUploadException()
    {
        var capture = CaptureWithSampleHash();
        capture.RecordUpload("captures/existing-key");

        var act = () => _sut.Handle(new UploadImageCommand(capture.Id.Value, SampleImage), CancellationToken.None);

        await act.Should().ThrowAsync<DuplicateUploadException>();
    }

    [Fact]
    public async Task Handle_WhenAlreadyUploaded_RecordsDuplicateUploadSecurityEvent()
    {
        var capture = CaptureWithSampleHash();
        capture.RecordUpload("captures/existing-key");

        try { await _sut.Handle(new UploadImageCommand(capture.Id.Value, SampleImage), CancellationToken.None); }
        catch (DuplicateUploadException) { }

        await _securityEvents.Received(1).AddAsync(
            Arg.Is<SecurityEvent>(e => e.EventType == SecurityEventType.DuplicateUpload),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenAlreadyUploaded_DoesNotStoreImage()
    {
        var capture = CaptureWithSampleHash();
        capture.RecordUpload("captures/existing-key");

        try { await _sut.Handle(new UploadImageCommand(capture.Id.Value, SampleImage), CancellationToken.None); }
        catch (DuplicateUploadException) { }

        _encryption.DidNotReceive().Encrypt(Arg.Any<byte[]>());
        await _imageStorage.DidNotReceive().StoreAsync(Arg.Any<Guid>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>());
    }
}
