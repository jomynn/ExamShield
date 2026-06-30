using ExamShield.Application.Commands.UploadImage;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.Services;
using ExamShield.Domain.ValueObjects;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.UploadImage;

public sealed class WatermarkDeviceIdTests
{
    private readonly ICaptureRepository _captures = Substitute.For<ICaptureRepository>();
    private readonly IImageStorage _storage = Substitute.For<IImageStorage>();
    private readonly IAuditLogRepository _auditLog = Substitute.For<IAuditLogRepository>();
    private readonly IWatermarkService _watermark = Substitute.For<IWatermarkService>();
    private readonly ISecurityEventRepository _security = Substitute.For<ISecurityEventRepository>();

    private static readonly byte[] FakeImage = [0xFF, 0xD8, 0xFF, 0xE0, 0x01, 0x02];

    private UploadImageCommandHandler MakeSut()
    {
        var encryption = Substitute.For<IImageEncryptionService>();
        encryption.Encrypt(Arg.Any<byte[]>()).Returns(c => (c.Arg<byte[]>(), Array.Empty<byte>()));
        var qrStamp = Substitute.For<IQrStampService>();
        qrStamp.Stamp(Arg.Any<byte[]>(), Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<string>())
            .Returns(c => c.ArgAt<byte[]>(0));
        return new(_captures, new HashVerificationService(), _storage, _auditLog, _watermark, _security, encryption, qrStamp);
    }

    [Fact]
    public async Task Handle_WatermarkPayloadContainsDeviceId()
    {
        var deviceId = DeviceId.New();
        var capture = Capture.Create(ExamId.New(), StudentId.New(), deviceId,
            new PageNumber(1),
            Hash.FromHex(Convert.ToHexString(
                System.Security.Cryptography.SHA256.HashData(FakeImage)).ToLower()),
            new Signature(new byte[64]));

        _captures.GetByIdAsync(Arg.Any<CaptureId>(), Arg.Any<CancellationToken>())
            .Returns(capture);
        _storage.StoreAsync(Arg.Any<Guid>(), Arg.Any<byte[]>(), Arg.Any<CancellationToken>())
            .Returns("key/1");
        _watermark.Embed(Arg.Any<byte[]>(), Arg.Any<WatermarkPayload>())
            .Returns(c => c.Arg<byte[]>());

        await MakeSut().Handle(new UploadImageCommand(capture.Id.Value, FakeImage), default);

        _watermark.Received(1).Embed(
            Arg.Any<byte[]>(),
            Arg.Is<WatermarkPayload>(p => p.DeviceId == deviceId.Value));
    }
}
