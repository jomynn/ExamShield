using ExamShield.Application.Queries.GetAuditLog;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using NSubstitute;

namespace ExamShield.UnitTests.Application.Queries;

public sealed class GetAuditLogDateRangeTests
{
    private readonly IAuditLogRepository _repo = Substitute.For<IAuditLogRepository>();
    private readonly GetAuditLogQueryHandler _sut;

    public GetAuditLogDateRangeTests() => _sut = new GetAuditLogQueryHandler(_repo);

    [Fact]
    public async Task Handle_PassesFromToToRepository()
    {
        var from = DateTimeOffset.UtcNow.AddHours(-1);
        var to   = DateTimeOffset.UtcNow;

        _repo.QueryAsync(null, 1, 50, null, null, from, to, default)
             .Returns((new List<AuditLog>(), 0));

        await _sut.Handle(new GetAuditLogQuery(From: from, To: to), default);

        await _repo.Received(1).QueryAsync(null, 1, 50, null, null, from, to, default);
    }

    [Fact]
    public async Task Handle_WithoutDateRange_PassesNullsToRepository()
    {
        _repo.QueryAsync(null, 1, 50, null, null, null, null, default)
             .Returns((new List<AuditLog>(), 0));

        await _sut.Handle(new GetAuditLogQuery(), default);

        await _repo.Received(1).QueryAsync(null, 1, 50, null, null, null, null, default);
    }
}
