using ExamShield.Application.Queries.GetOcrQueue;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetOcrQueue;

public sealed class GetOcrQueueQueryHandlerTests
{
    private readonly ICaptureRepository _captures = Substitute.For<ICaptureRepository>();
    private readonly GetOcrQueueQueryHandler _sut;

    public GetOcrQueueQueryHandlerTests() =>
        _sut = new GetOcrQueueQueryHandler(_captures);

    private static Capture MakeUploadedCapture()
    {
        var c = Capture.Create(
            new ExamId(Guid.NewGuid()), new StudentId(Guid.NewGuid()),
            new DeviceId(Guid.NewGuid()), new PageNumber(1),
            Hash.FromBytes(new byte[32]), new Signature(new byte[64]));
        c.RecordUpload("storage/key");
        return c;
    }

    [Fact]
    public async Task Handle_WithUploadedCaptures_ReturnsMappedItems()
    {
        var c = MakeUploadedCapture();
        _captures.ListByStatusAsync(CaptureStatus.Uploaded, Arg.Any<CancellationToken>())
                 .Returns(new[] { c });

        var result = await _sut.Handle(new GetOcrQueueQuery(), default);

        result.Items.Should().HaveCount(1);
        result.Items[0].CaptureId.Should().Be(c.Id.Value);
        result.Items[0].ExamId.Should().Be(c.ExamId.Value);
        result.Items[0].StudentId.Should().Be(c.StudentId.Value);
    }

    [Fact]
    public async Task Handle_QueriesUploadedStatus()
    {
        _captures.ListByStatusAsync(CaptureStatus.Uploaded, Arg.Any<CancellationToken>())
                 .Returns(Array.Empty<Capture>());

        await _sut.Handle(new GetOcrQueueQuery(), default);

        await _captures.Received(1).ListByStatusAsync(CaptureStatus.Uploaded, default);
    }

    [Fact]
    public async Task Handle_NoCaptures_ReturnsEmptyList()
    {
        _captures.ListByStatusAsync(CaptureStatus.Uploaded, Arg.Any<CancellationToken>())
                 .Returns(Array.Empty<Capture>());

        var result = await _sut.Handle(new GetOcrQueueQuery(), default);

        result.Items.Should().BeEmpty();
    }
}
