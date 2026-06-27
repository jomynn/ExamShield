using ExamShield.Application.Commands.TriggerBatchOcr;
using ExamShield.Application.Commands.TriggerOcr;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Commands.TriggerBatchOcr;

public sealed class TriggerBatchOcrCommandHandlerTests
{
    private readonly IExamRepository _exams = Substitute.For<IExamRepository>();
    private readonly ICaptureRepository _captures = Substitute.For<ICaptureRepository>();
    private readonly ISender _sender = Substitute.For<ISender>();
    private readonly TriggerBatchOcrCommandHandler _sut;

    private readonly Exam _exam;

    public TriggerBatchOcrCommandHandlerTests()
    {
        _exam = Exam.Create("Batch OCR Test", null, 5);
        _exam.Activate();
        _exams.GetByIdAsync(Arg.Any<ExamId>(), Arg.Any<CancellationToken>()).Returns(_exam);

        _sut = new TriggerBatchOcrCommandHandler(_exams, _captures, _sender);
    }

    private static Capture MakeCapture(ExamId examId, CaptureStatus status)
    {
        var hash = Hash.FromBytes(new byte[32]);
        var c = Capture.Create(examId, StudentId.New(), DeviceId.New(),
            new PageNumber(1), hash, new Signature(new byte[64]));
        if (status == CaptureStatus.Uploaded || status == CaptureStatus.Verified)
        {
            c.RecordUpload("storage/key");
            if (status == CaptureStatus.Verified) c.VerifyIntegrity(hash);
        }
        return c;
    }

    [Fact]
    public async Task Handle_WithUploadedCaptures_SendsOcrCommandForEach()
    {
        var c1 = MakeCapture(_exam.Id, CaptureStatus.Uploaded);
        var c2 = MakeCapture(_exam.Id, CaptureStatus.Uploaded);
        _captures.ListByExamIdAsync(_exam.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { c1, c2 });
        _sender.Send(Arg.Any<TriggerOcrCommand>(), Arg.Any<CancellationToken>())
            .Returns(new TriggerOcrResult(Guid.NewGuid(), OcrStatus.Completed, false));

        var result = await _sut.Handle(new TriggerBatchOcrCommand(_exam.Id.Value), default);

        Assert.Equal(2, result.Queued);
        Assert.Equal(0, result.Skipped);
        await _sender.Received(2).Send(Arg.Any<TriggerOcrCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_SkipsCreatedCaptures()
    {
        var uploaded = MakeCapture(_exam.Id, CaptureStatus.Uploaded);
        var created  = MakeCapture(_exam.Id, CaptureStatus.Created);
        _captures.ListByExamIdAsync(_exam.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { uploaded, created });
        _sender.Send(Arg.Any<TriggerOcrCommand>(), Arg.Any<CancellationToken>())
            .Returns(new TriggerOcrResult(Guid.NewGuid(), OcrStatus.Completed, false));

        var result = await _sut.Handle(new TriggerBatchOcrCommand(_exam.Id.Value), default);

        Assert.Equal(1, result.Queued);
        Assert.Equal(1, result.Skipped);
        await _sender.Received(1).Send(Arg.Any<TriggerOcrCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_IncludesVerifiedCaptures()
    {
        var verified = MakeCapture(_exam.Id, CaptureStatus.Verified);
        _captures.ListByExamIdAsync(_exam.Id, Arg.Any<CancellationToken>())
            .Returns(new[] { verified });
        _sender.Send(Arg.Any<TriggerOcrCommand>(), Arg.Any<CancellationToken>())
            .Returns(new TriggerOcrResult(Guid.NewGuid(), OcrStatus.Completed, false));

        var result = await _sut.Handle(new TriggerBatchOcrCommand(_exam.Id.Value), default);

        Assert.Equal(1, result.Queued);
        await _sender.Received(1).Send(Arg.Any<TriggerOcrCommand>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WhenExamNotFound_ThrowsKeyNotFoundException()
    {
        _exams.GetByIdAsync(Arg.Any<ExamId>(), Arg.Any<CancellationToken>())
            .Returns((Exam?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.Handle(new TriggerBatchOcrCommand(Guid.NewGuid()), default));
    }

    [Fact]
    public async Task Handle_WithNoEligibleCaptures_ReturnsZeroQueued()
    {
        _captures.ListByExamIdAsync(_exam.Id, Arg.Any<CancellationToken>())
            .Returns(Array.Empty<Capture>());

        var result = await _sut.Handle(new TriggerBatchOcrCommand(_exam.Id.Value), default);

        Assert.Equal(0, result.Queued);
        await _sender.DidNotReceive().Send(Arg.Any<TriggerOcrCommand>(), Arg.Any<CancellationToken>());
    }
}
