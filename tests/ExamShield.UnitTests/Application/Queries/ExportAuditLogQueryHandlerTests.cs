using ExamShield.Application.Queries.ExportAuditLog;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries;

public sealed class ExportAuditLogQueryHandlerTests
{
    private readonly IAuditLogRepository _repo = Substitute.For<IAuditLogRepository>();
    private readonly ExportAuditLogQueryHandler _sut;

    public ExportAuditLogQueryHandlerTests()
    {
        _sut = new ExportAuditLogQueryHandler(_repo);
        _repo.ExportAsync(
                Arg.Any<CaptureId?>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<DateTimeOffset?>(),
                Arg.Any<CancellationToken>())
            .Returns(new List<AuditLog>());
    }

    [Fact]
    public async Task Handle_WithNoEntries_ReturnsCsvWithHeaderOnly()
    {
        var result = await _sut.Handle(new ExportAuditLogQuery(null, null, null), default);

        var lines = result.Csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(1);
        lines[0].Should().Contain("Id");
        lines[0].Should().Contain("Action");
        lines[0].Should().Contain("OccurredAt");
    }

    [Fact]
    public async Task Handle_WithEntries_ReturnsCsvWithDataRows()
    {
        var entry = MakeAuditLog(AuditAction.CaptureRegistered);
        _repo.ExportAsync(null, null, null, Arg.Any<CancellationToken>())
            .Returns(new List<AuditLog> { entry });

        var result = await _sut.Handle(new ExportAuditLogQuery(null, null, null), default);

        var lines = result.Csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2);
        lines[1].Should().Contain("CaptureRegistered");
    }

    [Fact]
    public async Task Handle_PassesCaptureIdFilterToRepository()
    {
        var captureId = Guid.NewGuid();

        await _sut.Handle(new ExportAuditLogQuery(captureId, null, null), default);

        await _repo.Received(1).ExportAsync(
            Arg.Is<CaptureId?>(id => id != null && id.Value == captureId),
            null, null, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Handle_PassesDateRangeFilterToRepository()
    {
        var from = DateTimeOffset.UtcNow.AddDays(-7);
        var to = DateTimeOffset.UtcNow;

        await _sut.Handle(new ExportAuditLogQuery(null, from, to), default);

        await _repo.Received(1).ExportAsync(
            null,
            Arg.Is<DateTimeOffset?>(d => d == from),
            Arg.Is<DateTimeOffset?>(d => d == to),
            Arg.Any<CancellationToken>());
    }

    private static AuditLog MakeAuditLog(AuditAction action) =>
        AuditLog.Record(action, captureId: CaptureId.New());
}
