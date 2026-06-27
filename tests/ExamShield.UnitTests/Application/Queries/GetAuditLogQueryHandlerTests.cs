using ExamShield.Application.Queries.GetAuditLog;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries;

public sealed class GetAuditLogQueryHandlerTests
{
    private readonly IAuditLogRepository _repository = Substitute.For<IAuditLogRepository>();
    private readonly GetAuditLogQueryHandler _sut;

    public GetAuditLogQueryHandlerTests() =>
        _sut = new GetAuditLogQueryHandler(_repository);

    [Fact]
    public async Task Handle_WithNoFilters_ReturnsAllEntries()
    {
        var captureId = CaptureId.New();
        var entries = new List<AuditLog>
        {
            AuditLog.Record(AuditAction.CaptureRegistered, captureId: captureId)
        };
        _repository.QueryAsync(null, 1, 50, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns((entries.AsReadOnly() as IReadOnlyList<AuditLog>, 1));

        var result = await _sut.Handle(new GetAuditLogQuery(), CancellationToken.None);

        result.Entries.Should().HaveCount(1);
        result.TotalCount.Should().Be(1);
        result.Entries[0].Action.Should().Be(nameof(AuditAction.CaptureRegistered));
        result.Entries[0].CaptureId.Should().Be(captureId.Value);
    }

    [Fact]
    public async Task Handle_WithCaptureIdFilter_PassesFilterToRepository()
    {
        var captureId = CaptureId.New();
        _repository.QueryAsync(Arg.Any<CaptureId>(), 1, 50, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns((new List<AuditLog>().AsReadOnly() as IReadOnlyList<AuditLog>, 0));

        await _sut.Handle(new GetAuditLogQuery(CaptureId: captureId.Value), CancellationToken.None);

        await _repository.Received(1).QueryAsync(
            Arg.Is<CaptureId>(id => id == captureId),
            1, 50, null, null, null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_WithPagination_PassesPaginationToRepository()
    {
        _repository.QueryAsync(null, 2, 10, null, null, null, null, Arg.Any<CancellationToken>())
            .Returns((new List<AuditLog>().AsReadOnly() as IReadOnlyList<AuditLog>, 0));

        await _sut.Handle(new GetAuditLogQuery(Page: 2, PageSize: 10), CancellationToken.None);

        await _repository.Received(1).QueryAsync(null, 2, 10, null, null, null, null, Arg.Any<CancellationToken>());
    }
}
