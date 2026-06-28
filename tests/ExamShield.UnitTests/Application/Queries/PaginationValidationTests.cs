using ExamShield.Application.Queries.GetCaptures;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Interfaces;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries;

public sealed class PaginationValidationTests
{
    private readonly ICaptureRepository _captures = Substitute.For<ICaptureRepository>();
    private readonly GetCapturesQueryHandler _sut;

    public PaginationValidationTests() => _sut = new GetCapturesQueryHandler(_captures);

    [Theory]
    [InlineData(0, 50)]
    [InlineData(-1, 50)]
    public async Task Handle_WhenPageIsLessThanOne_ThrowsArgumentOutOfRangeException(int page, int pageSize)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _sut.Handle(new GetCapturesQuery(Page: page, PageSize: pageSize), default));
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, -1)]
    [InlineData(1, 201)]
    public async Task Handle_WhenPageSizeIsOutOfRange_ThrowsArgumentOutOfRangeException(int page, int pageSize)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            _sut.Handle(new GetCapturesQuery(Page: page, PageSize: pageSize), default));
    }

    [Fact]
    public async Task Handle_WithValidPagination_CallsRepository()
    {
        _captures.ListPagedAsync(1, 50, null, null, null, null, null, default)
            .Returns((new List<Capture>() as IReadOnlyList<Capture>, 0));

        await _sut.Handle(new GetCapturesQuery(Page: 1, PageSize: 50), default);

        await _captures.Received(1).ListPagedAsync(1, 50, null, null, null, null, null, default);
    }
}
