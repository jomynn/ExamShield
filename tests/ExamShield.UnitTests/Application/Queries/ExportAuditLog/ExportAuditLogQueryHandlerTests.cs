using ExamShield.Application.Queries.ExportAuditLog;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.ExportAuditLog;

public sealed class ExportAuditLogQueryHandlerTests
{
    private readonly IAuditLogRepository _repo = Substitute.For<IAuditLogRepository>();
    private readonly ExportAuditLogQueryHandler _sut;

    public ExportAuditLogQueryHandlerTests() => _sut = new(_repo);

    [Fact]
    public async Task Handle_EmptyEntries_ReturnsCsvWithHeaderOnly()
    {
        IReadOnlyList<AuditLog> empty = [];
        _repo.ExportAsync(Arg.Any<CaptureId?>(), Arg.Any<DateTimeOffset?>(),
            Arg.Any<DateTimeOffset?>(), default).Returns(empty);

        var result = await _sut.Handle(new(null, null, null), default);

        result.Csv.Should().Contain("Id,Action,CaptureId");
        result.Csv.Trim().Split('\n').Should().HaveCount(1);
    }

    [Fact]
    public async Task Handle_WithEntry_ReturnsRowInCsv()
    {
        var entry = AuditLog.Record(AuditAction.HashVerified);
        IReadOnlyList<AuditLog> list = [entry];
        _repo.ExportAsync(Arg.Any<CaptureId?>(), Arg.Any<DateTimeOffset?>(),
            Arg.Any<DateTimeOffset?>(), default).Returns(list);

        var result = await _sut.Handle(new(null, null, null), default);

        result.Csv.Should().Contain("HashVerified");
    }

    [Fact]
    public async Task Handle_FilenameContainsDatePattern()
    {
        IReadOnlyList<AuditLog> empty = [];
        _repo.ExportAsync(Arg.Any<CaptureId?>(), Arg.Any<DateTimeOffset?>(),
            Arg.Any<DateTimeOffset?>(), default).Returns(empty);

        var result = await _sut.Handle(new(null, null, null), default);

        result.Filename.Should().StartWith("audit-");
        result.Filename.Should().EndWith(".csv");
    }

    [Fact]
    public async Task Handle_CommaInReason_EscapesWithQuotes()
    {
        var entry = AuditLog.Record(AuditAction.TamperingDetected, reason: "Tampered, hash mismatch");
        IReadOnlyList<AuditLog> list = [entry];
        _repo.ExportAsync(Arg.Any<CaptureId?>(), Arg.Any<DateTimeOffset?>(),
            Arg.Any<DateTimeOffset?>(), default).Returns(list);

        var result = await _sut.Handle(new(null, null, null), default);

        result.Csv.Should().Contain("\"Tampered, hash mismatch\"");
    }
}
