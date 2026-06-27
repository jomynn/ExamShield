using ExamShield.Application.Commands.RegisterCapture;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands;

public sealed class RegisterCaptureCommandHandlerTests
{
    private readonly ICaptureRepository _repository = Substitute.For<ICaptureRepository>();
    private readonly IDeviceRepository _devices = Substitute.For<IDeviceRepository>();
    private readonly ISignatureVerificationService _sigService = Substitute.For<ISignatureVerificationService>();
    private readonly IAuditLogRepository _auditLog = Substitute.For<IAuditLogRepository>();
    private readonly IExamRepository _exams = Substitute.For<IExamRepository>();
    private readonly RegisterCaptureCommandHandler _sut;

    public RegisterCaptureCommandHandlerTests()
    {
        // Default: valid device + valid signature + active exam
        var device = Device.Register("Test Device", new PublicKey(new byte[] { 0x04 }));
        _devices.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>()).Returns(device);
        _sigService.Verify(Arg.Any<Hash>(), Arg.Any<Signature>(), Arg.Any<PublicKey>()).Returns(true);

        var activeExam = Exam.Create("Test Exam", null, 10);
        activeExam.Activate();
        _exams.GetByIdAsync(Arg.Any<ExamId>(), Arg.Any<CancellationToken>()).Returns(activeExam);

        _sut = new RegisterCaptureCommandHandler(_repository, _devices, _sigService, _auditLog, _exams);
    }

    private static RegisterCaptureCommand ValidCommand() => new(
        ExamId: Guid.NewGuid(),
        StudentId: Guid.NewGuid(),
        DeviceId: Guid.NewGuid(),
        PageNumber: 1,
        HashHex: new string('a', 64),
        SignatureBytes: new byte[64]
    );

    [Fact]
    public async Task Handle_WithValidCommand_ReturnsCaptureId()
    {
        var result = await _sut.Handle(ValidCommand(), CancellationToken.None);

        result.CaptureId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task Handle_WithValidCommand_SavesCaptureToRepository()
    {
        await _sut.Handle(ValidCommand(), CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<Capture>(c => c.Status == CaptureStatus.Created),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithValidCommand_CaptureHasCorrectExamId()
    {
        var command = ValidCommand();

        await _sut.Handle(command, CancellationToken.None);

        await _repository.Received(1).AddAsync(
            Arg.Is<Capture>(c => c.ExamId == new ExamId(command.ExamId)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithValidCommand_AppendsCaptureRegisteredAuditEntry()
    {
        await _sut.Handle(ValidCommand(), CancellationToken.None);

        await _auditLog.Received(1).AppendAsync(
            Arg.Is<AuditLog>(e => e.Action == AuditAction.CaptureRegistered),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithValidCommand_AuditEntryContainsCaptureId()
    {
        var result = await _sut.Handle(ValidCommand(), CancellationToken.None);

        await _auditLog.Received(1).AppendAsync(
            Arg.Is<AuditLog>(e => e.CaptureId != null && e.CaptureId.Value == result.CaptureId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenDeviceNotFound_ThrowsDeviceNotFoundException()
    {
        _devices.GetByIdAsync(Arg.Any<DeviceId>(), Arg.Any<CancellationToken>()).Returns((Device?)null);

        var act = () => _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<DeviceNotFoundException>();
    }

    [Fact]
    public async Task Handle_WhenSignatureInvalid_ThrowsInvalidSignatureException()
    {
        _sigService.Verify(Arg.Any<Hash>(), Arg.Any<Signature>(), Arg.Any<PublicKey>()).Returns(false);

        var act = () => _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<InvalidSignatureException>();
    }

    [Fact]
    public async Task Handle_WithInvalidPageNumber_ThrowsArgumentException()
    {
        var act = () => _sut.Handle(ValidCommand() with { PageNumber = 0 }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_WithInvalidHashHex_ThrowsArgumentException()
    {
        var act = () => _sut.Handle(ValidCommand() with { HashHex = "not-a-valid-hex" }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_WithEmptySignatureBytes_ThrowsArgumentException()
    {
        var act = () => _sut.Handle(ValidCommand() with { SignatureBytes = Array.Empty<byte>() }, CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task Handle_WhenExamIsInDraft_ThrowsExamNotActiveException()
    {
        var draftExam = Exam.Create("Draft Exam", null, 10);
        _exams.GetByIdAsync(Arg.Any<ExamId>(), Arg.Any<CancellationToken>()).Returns(draftExam);

        var act = () => _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<ExamNotActiveException>();
    }

    [Fact]
    public async Task Handle_WhenExamIsClosed_ThrowsExamNotActiveException()
    {
        var closedExam = Exam.Create("Closed Exam", null, 10);
        closedExam.Activate();
        closedExam.Close();
        _exams.GetByIdAsync(Arg.Any<ExamId>(), Arg.Any<CancellationToken>()).Returns(closedExam);

        var act = () => _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<ExamNotActiveException>();
    }

    [Fact]
    public async Task Handle_WhenExamNotFound_ThrowsKeyNotFoundException()
    {
        _exams.GetByIdAsync(Arg.Any<ExamId>(), Arg.Any<CancellationToken>()).Returns((Exam?)null);

        var act = () => _sut.Handle(ValidCommand(), CancellationToken.None);

        await act.Should().ThrowAsync<KeyNotFoundException>();
    }
}
