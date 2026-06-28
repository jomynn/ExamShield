using ExamShield.Application.Queries.GetCaptureById;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetCaptureById;

public sealed class GetCaptureByIdQueryHandlerTests
{
    private readonly ICaptureRepository _captures = Substitute.For<ICaptureRepository>();
    private readonly GetCaptureByIdQueryHandler _sut;

    public GetCaptureByIdQueryHandlerTests() =>
        _sut = new GetCaptureByIdQueryHandler(_captures);

    private static Capture MakeCapture() =>
        Capture.Create(
            new ExamId(Guid.NewGuid()), new StudentId(Guid.NewGuid()),
            new DeviceId(Guid.NewGuid()), new PageNumber(1),
            Hash.FromBytes(new byte[32]), new Signature(new byte[64]));

    [Fact]
    public async Task Handle_ExistingCapture_ReturnsMappedDto()
    {
        var capture = MakeCapture();
        _captures.GetByIdAsync(capture.Id, Arg.Any<CancellationToken>()).Returns(capture);

        var result = await _sut.Handle(new GetCaptureByIdQuery(capture.Id.Value), default);

        result.Should().NotBeNull();
        result!.CaptureId.Should().Be(capture.Id.Value);
        result.ExamId.Should().Be(capture.ExamId.Value);
        result.StudentId.Should().Be(capture.StudentId.Value);
        result.PageNumber.Should().Be(1);
        result.Status.Should().Be(capture.Status.ToString());
    }

    [Fact]
    public async Task Handle_MissingCapture_ReturnsNull()
    {
        _captures.GetByIdAsync(Arg.Any<CaptureId>(), Arg.Any<CancellationToken>())
                 .Returns((Capture?)null);

        var result = await _sut.Handle(new GetCaptureByIdQuery(Guid.NewGuid()), default);

        result.Should().BeNull();
    }
}
