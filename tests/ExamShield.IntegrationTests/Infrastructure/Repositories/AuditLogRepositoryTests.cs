using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.ValueObjects;
using ExamShield.Infrastructure.Persistence;
using ExamShield.Infrastructure.Persistence.Repositories;
using ExamShield.Infrastructure.Security;
using ExamShield.Infrastructure.Storage;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ExamShield.IntegrationTests.Infrastructure.Repositories;

public sealed class AuditLogRepositoryTests : IAsyncLifetime
{
    private SqliteConnection _connection = null!;
    private ExamShieldDbContext _context = null!;
    private AuditLogRepository _sut = null!;

    public async Task InitializeAsync()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        await _connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ExamShieldDbContext>()
            .UseSqlite(_connection)
            .Options;

        _context = new ExamShieldDbContext(options);
        await _context.Database.EnsureCreatedAsync();
        _sut = new AuditLogRepository(_context, new EcdsaServerSigningService(null), new NullAuditLogArchiveService());
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private static AuditLog MakeEntry(AuditAction action = AuditAction.CaptureRegistered, CaptureId? captureId = null) =>
        AuditLog.Record(action, captureId: captureId);

    [Fact]
    public async Task AppendAsync_ThenQuery_ReturnsPersistedEntry()
    {
        var entry = MakeEntry();

        await _sut.AppendAsync(entry);
        var (entries, total) = await _sut.QueryAsync(null, 1, 50);

        total.Should().Be(1);
        entries[0].Id.Should().Be(entry.Id);
        entries[0].Action.Should().Be(entry.Action);
    }

    [Fact]
    public async Task AppendAsync_PreservesAllFields()
    {
        var captureId = CaptureId.New();
        var entry = AuditLog.Record(
            AuditAction.TamperingDetected,
            captureId: captureId,
            userId: "user-1",
            ipAddress: "192.168.1.1",
            reason: "hash mismatch");

        await _sut.AppendAsync(entry);
        _context.ChangeTracker.Clear();

        var (entries, _) = await _sut.QueryAsync(captureId, 1, 50);

        var loaded = entries.Single();
        loaded.Action.Should().Be(AuditAction.TamperingDetected);
        loaded.CaptureId.Should().Be(captureId);
        loaded.UserId.Should().Be("user-1");
        loaded.IpAddress.Should().Be("192.168.1.1");
        loaded.Reason.Should().Be("hash mismatch");
        loaded.OccurredAt.Should().BeCloseTo(entry.OccurredAt, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task QueryAsync_FiltersByCaptureId()
    {
        var captureId = CaptureId.New();
        await _sut.AppendAsync(MakeEntry(captureId: captureId));
        await _sut.AppendAsync(MakeEntry(captureId: CaptureId.New()));

        var (entries, total) = await _sut.QueryAsync(captureId, 1, 50);

        total.Should().Be(1);
        entries.Should().AllSatisfy(e => e.CaptureId.Should().Be(captureId));
    }

    [Fact]
    public async Task QueryAsync_WithNullCaptureId_ReturnsAll()
    {
        await _sut.AppendAsync(MakeEntry(captureId: CaptureId.New()));
        await _sut.AppendAsync(MakeEntry(captureId: CaptureId.New()));
        await _sut.AppendAsync(MakeEntry());

        var (_, total) = await _sut.QueryAsync(null, 1, 50);

        total.Should().Be(3);
    }

    [Fact]
    public async Task QueryAsync_ReturnsEntriesOrderedByOccurredAtDescending()
    {
        await _sut.AppendAsync(MakeEntry());
        await Task.Delay(10);
        await _sut.AppendAsync(MakeEntry());

        var (entries, _) = await _sut.QueryAsync(null, 1, 50);

        entries[0].OccurredAt.Should().BeOnOrAfter(entries[1].OccurredAt);
    }

    [Fact]
    public async Task QueryAsync_RespectsPageSize()
    {
        for (var i = 0; i < 5; i++)
            await _sut.AppendAsync(MakeEntry());

        var (entries, total) = await _sut.QueryAsync(null, 1, 3);

        total.Should().Be(5);
        entries.Should().HaveCount(3);
    }

    [Fact]
    public async Task QueryAsync_RespectsPageNumber()
    {
        for (var i = 0; i < 4; i++)
            await _sut.AppendAsync(MakeEntry());

        var (page2, total) = await _sut.QueryAsync(null, 2, 3);

        total.Should().Be(4);
        page2.Should().HaveCount(1);
    }
}
