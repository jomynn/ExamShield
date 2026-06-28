using ExamShield.Application.Queries.GetCaptures;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetCaptures;

public sealed class GetCapturesQueryHandlerTests
{
    private readonly ICaptureRepository _captures = Substitute.For<ICaptureRepository>();
    private readonly GetCapturesQueryHandler _sut;

    public GetCapturesQueryHandlerTests() => _sut = new(_captures);

    private static GetCapturesQuery PageQuery() => new(1, 20);

    private static Capture MakeCapture()
    {
        var exam = new ExamId(Guid.NewGuid());
        var student = new StudentId(Guid.NewGuid());
        var device = new DeviceId(Guid.NewGuid());
        var hash = Hash.FromBytes(new byte[32]);
        var sig = new Signature(new byte[64]);
        return Capture.Create(exam, student, device, new PageNumber(1), hash, sig);
    }

    [Fact]
    public async Task Handle_ReturnsCapturesDtos()
    {
        IReadOnlyList<Capture> list = [MakeCapture()];
        _captures.ListPagedAsync(1, 20, null, null, null, null, null, default).Returns((list, 1));

        var result = await _sut.Handle(PageQuery(), default);

        result.Captures.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_MapsDtoStatusField()
    {
        IReadOnlyList<Capture> list = [MakeCapture()];
        _captures.ListPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<ExamId?>(),
            Arg.Any<CaptureStatus?>(), Arg.Any<DeviceId?>(), Arg.Any<StudentId?>(),
            Arg.Any<UserId?>(), default).Returns((list, 1));

        var result = await _sut.Handle(PageQuery(), default);

        result.Captures[0].Status.Should().Be("Created");
    }

    [Fact]
    public async Task Handle_EmptyResult_ReturnsZeroTotal()
    {
        IReadOnlyList<Capture> empty = [];
        _captures.ListPagedAsync(Arg.Any<int>(), Arg.Any<int>(), Arg.Any<ExamId?>(),
            Arg.Any<CaptureStatus?>(), Arg.Any<DeviceId?>(), Arg.Any<StudentId?>(),
            Arg.Any<UserId?>(), default).Returns((empty, 0));

        var result = await _sut.Handle(PageQuery(), default);

        result.Captures.Should().BeEmpty();
        result.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_PropagatesPaginationMeta()
    {
        IReadOnlyList<Capture> list = [MakeCapture()];
        _captures.ListPagedAsync(3, 15, Arg.Any<ExamId?>(), Arg.Any<CaptureStatus?>(),
            Arg.Any<DeviceId?>(), Arg.Any<StudentId?>(), Arg.Any<UserId?>(), default).Returns((list, 200));

        var result = await _sut.Handle(new(3, 15), default);

        result.Page.Should().Be(3);
        result.PageSize.Should().Be(15);
        result.TotalCount.Should().Be(200);
    }
}
