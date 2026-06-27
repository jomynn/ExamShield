using ExamShield.Application.Queries.PublicVerifyCapture;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.Services;
using ExamShield.Domain.ValueObjects;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries;

public sealed class PublicVerifyByHashTests
{
    private readonly ICaptureRepository _captures   = Substitute.For<ICaptureRepository>();
    private readonly IDeviceRepository  _devices    = Substitute.For<IDeviceRepository>();
    private readonly IImageStorage      _storage    = Substitute.For<IImageStorage>();
    private readonly HashVerificationService _hash  = new();
    private readonly ISignatureVerificationService _sig = Substitute.For<ISignatureVerificationService>();
    private readonly IWatermarkService  _watermark  = Substitute.For<IWatermarkService>();
    private readonly PublicVerifyCaptureQueryHandler _sut;

    public PublicVerifyByHashTests()
    {
        _sut = new PublicVerifyCaptureQueryHandler(
            _captures, _devices, _storage, _hash, _sig, _watermark);
    }

    [Fact]
    public async Task Handle_WithHashHex_LooksUpCaptureByHash()
    {
        var hashHex = new string('a', 64);
        var capture = CreateCapture(hashHex);
        _captures.FindByHashAsync(Arg.Is<Hash>(h => h.Hex == hashHex), Arg.Any<CancellationToken>())
                 .Returns(capture);

        var result = await _sut.Handle(
            new PublicVerifyCaptureQuery(null, HashHex: hashHex), default);

        Assert.Equal(capture.Id.Value, result.CaptureId);
        await _captures.Received(1).FindByHashAsync(
            Arg.Is<Hash>(h => h.Hex == hashHex), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithHashHex_NotFound_ThrowsCaptureNotFoundException()
    {
        _captures.FindByHashAsync(Arg.Any<Hash>(), Arg.Any<CancellationToken>())
                 .Returns((Capture?)null);

        await Assert.ThrowsAnyAsync<Exception>(() =>
            _sut.Handle(new PublicVerifyCaptureQuery(null, HashHex: new string('a', 64)), default));
    }

    [Fact]
    public async Task Handle_WithCaptureId_StillLooksUpById()
    {
        var capture = CreateCapture(new string('c', 64));
        _captures.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>())
                 .Returns(capture);

        var result = await _sut.Handle(
            new PublicVerifyCaptureQuery(capture.Id.Value), default);

        Assert.Equal(capture.Id.Value, result.CaptureId);
        await _captures.Received(1).GetByIdAsync(capture.Id, Arg.Any<CancellationToken>());
        await _captures.DidNotReceive().FindByHashAsync(Arg.Any<Hash>(), Arg.Any<CancellationToken>());
    }

    private static Capture CreateCapture(string hashHex)
    {
        var examId     = new ExamId(Guid.NewGuid());
        var studentId  = new StudentId(Guid.NewGuid());
        var deviceId   = new DeviceId(Guid.NewGuid());
        var pageNumber = new PageNumber(1);
        var hash       = Hash.FromHex(hashHex);
        var signature  = new Signature(new byte[64]);
        return Capture.Create(examId, studentId, deviceId, pageNumber, hash, signature);
    }
}
