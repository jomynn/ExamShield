using ExamShield.Application.Queries.GetAuditLog;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using FluentAssertions;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries.GetAuditLog;

public sealed class GetAuditLogQueryHandlerTests
{
    private readonly IAuditLogRepository _repo = Substitute.For<IAuditLogRepository>();
    private readonly GetAuditLogQueryHandler _sut;

    public GetAuditLogQueryHandlerTests() => _sut = new(_repo);

    // GetAuditLogQuery(Guid? CaptureId, int Page, int PageSize, string? Action, string? UserId, DateTimeOffset? From, DateTimeOffset? To)
    private static GetAuditLogQuery BasicQuery() => new(null, 1, 20, null, null, null, null);

    private static AuditLog MakeEntry() => AuditLog.Record(AuditAction.CaptureRegistered);

    [Fact]
    public async Task Handle_ReturnsDtosAndTotal()
    {
        IReadOnlyList<AuditLog> list = [MakeEntry(), MakeEntry()];
        _repo.QueryAsync(null, 1, 20, null, null, null, null, default).Returns((list, 2));

        var result = await _sut.Handle(BasicQuery(), default);

        result.Entries.Should().HaveCount(2);
        result.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WithActionFilter_ParsesEnumAndForwards()
    {
        IReadOnlyList<AuditLog> empty = [];
        _repo.QueryAsync(null, 1, 20, AuditAction.CaptureRegistered, null, null, null, default)
            .Returns((empty, 0));

        await _sut.Handle(new(null, 1, 20, "CaptureRegistered", null, null, null), default);

        await _repo.Received(1).QueryAsync(
            null, 1, 20, AuditAction.CaptureRegistered, null, null, null, default);
    }

    [Fact]
    public async Task Handle_WithInvalidActionString_PassesNullAction()
    {
        IReadOnlyList<AuditLog> empty = [];
        _repo.QueryAsync(null, 1, 20, null, null, null, null, default).Returns((empty, 0));

        await _sut.Handle(new(null, 1, 20, "NOTANACTION", null, null, null), default);

        await _repo.Received(1).QueryAsync(null, 1, 20, null, null, null, null, default);
    }

    [Fact]
    public async Task Handle_MapsDtoFieldsCorrectly()
    {
        var entry = AuditLog.Record(AuditAction.HashVerified, null, "user-42", "127.0.0.1");
        IReadOnlyList<AuditLog> list = [entry];
        _repo.QueryAsync(null, 1, 20, null, null, null, null, default).Returns((list, 1));

        var result = await _sut.Handle(BasicQuery(), default);

        var dto = result.Entries[0];
        dto.Action.Should().Be("HashVerified");
        dto.UserId.Should().Be("user-42");
        dto.IpAddress.Should().Be("127.0.0.1");
    }

    [Fact]
    public async Task Handle_WithCaptureId_ForwardsCaptureIdFilter()
    {
        var captureId = Guid.NewGuid();
        IReadOnlyList<AuditLog> empty = [];
        _repo.QueryAsync(Arg.Is<CaptureId?>(c => c!.Value == captureId),
            1, 20, null, null, null, null, default).Returns((empty, 0));

        await _sut.Handle(new(captureId, 1, 20, null, null, null, null), default);

        await _repo.Received(1).QueryAsync(
            Arg.Is<CaptureId?>(c => c!.Value == captureId),
            1, 20, null, null, null, null, default);
    }
}
