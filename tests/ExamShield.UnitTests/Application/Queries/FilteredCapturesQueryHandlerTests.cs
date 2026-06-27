using ExamShield.Application.Queries.GetCaptures;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries;

public sealed class FilteredCapturesQueryHandlerTests
{
    private readonly ICaptureRepository _repo = Substitute.For<ICaptureRepository>();
    private readonly GetCapturesQueryHandler _sut;

    public FilteredCapturesQueryHandlerTests() => _sut = new GetCapturesQueryHandler(_repo);

    private static Capture MakeCapture(ExamId? examId = null) =>
        Capture.Create(examId ?? ExamId.New(), StudentId.New(), DeviceId.New(),
            new PageNumber(1), Hash.FromBytes(new byte[32]), new Signature(new byte[64]));

    [Fact]
    public async Task Handle_WithExamIdFilter_PassesExamIdToRepository()
    {
        var examId = ExamId.New();
        var capture = MakeCapture(examId);
        _repo.ListPagedAsync(1, 50, examId, null, Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Capture>)new[] { capture }, 1));

        var result = await _sut.Handle(
            new GetCapturesQuery(Page: 1, PageSize: 50, ExamId: examId.Value),
            CancellationToken.None);

        result.Captures.Should().HaveCount(1);
        result.Captures[0].ExamId.Should().Be(examId.Value);
    }

    [Fact]
    public async Task Handle_WithStatusFilter_PassesStatusToRepository()
    {
        var capture = MakeCapture();
        _repo.ListPagedAsync(1, 50, null, CaptureStatus.Verified, Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Capture>)new[] { capture }, 1));

        var result = await _sut.Handle(
            new GetCapturesQuery(Page: 1, PageSize: 50, Status: CaptureStatus.Verified),
            CancellationToken.None);

        result.Captures.Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WithNoFilters_PassesNullsToRepository()
    {
        _repo.ListPagedAsync(1, 50, null, null, Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Capture>)Array.Empty<Capture>(), 0));

        var result = await _sut.Handle(
            new GetCapturesQuery(Page: 1, PageSize: 50),
            CancellationToken.None);

        result.Captures.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_WithExamIdFilter_MapsCapturesCorrectly()
    {
        var examId = ExamId.New();
        var capture = MakeCapture(examId);
        _repo.ListPagedAsync(1, 50, examId, null, Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<Capture>)new[] { capture }, 1));

        var result = await _sut.Handle(
            new GetCapturesQuery(Page: 1, PageSize: 50, ExamId: examId.Value),
            CancellationToken.None);

        result.Captures[0].ExamId.Should().Be(examId.Value);
        result.TotalCount.Should().Be(1);
    }
}
