using ExamShield.Application.Queries.GetCaptures;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;
using Xunit;

namespace ExamShield.UnitTests.Application.Queries;

public sealed class PaginatedCapturesQueryHandlerTests
{
    private readonly ICaptureRepository _repo = Substitute.For<ICaptureRepository>();
    private readonly GetCapturesQueryHandler _sut;

    public PaginatedCapturesQueryHandlerTests()
        => _sut = new GetCapturesQueryHandler(_repo);

    private static Capture MakeCapture() =>
        Capture.Create(ExamId.New(), StudentId.New(), DeviceId.New(),
            new PageNumber(1), Hash.FromBytes(new byte[32]), new Signature(new byte[64]));

    [Fact]
    public async Task Handle_ReturnsPagedSlice_WhenMultipleCapturesExist()
    {
        var all = Enumerable.Range(0, 5).Select(_ => MakeCapture()).ToList();
        _repo.ListPagedAsync(2, 2, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns((all.Skip(2).Take(2).ToList().AsReadOnly() as IReadOnlyList<Capture>, 5));

        var result = await _sut.Handle(new GetCapturesQuery(Page: 2, PageSize: 2), CancellationToken.None);

        result.Captures.Should().HaveCount(2);
        result.TotalCount.Should().Be(5);
        result.Page.Should().Be(2);
        result.PageSize.Should().Be(2);
    }

    [Fact]
    public async Task Handle_TotalPagesCalculatedCorrectly()
    {
        _repo.ListPagedAsync(1, 3, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns((new List<Capture>().AsReadOnly() as IReadOnlyList<Capture>, 7));

        var result = await _sut.Handle(new GetCapturesQuery(Page: 1, PageSize: 3), CancellationToken.None);

        result.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task Handle_WithPageSize1_ReturnsSingleItem()
    {
        var capture = MakeCapture();
        _repo.ListPagedAsync(1, 1, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns((new List<Capture> { capture }.AsReadOnly() as IReadOnlyList<Capture>, 10));

        var result = await _sut.Handle(new GetCapturesQuery(Page: 1, PageSize: 1), CancellationToken.None);

        result.Captures.Should().HaveCount(1);
        result.Captures[0].CaptureId.Should().Be(capture.Id.Value);
    }
}
