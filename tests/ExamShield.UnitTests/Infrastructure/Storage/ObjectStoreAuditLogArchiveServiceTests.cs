using ExamShield.Application.Interfaces;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.ValueObjects;
using ExamShield.Infrastructure.Storage;
using FluentAssertions;
using System.Text.Json;
using Xunit;

namespace ExamShield.UnitTests.Infrastructure.Storage;

public sealed class ObjectStoreAuditLogArchiveServiceTests
{
    private static AuditLog MakeEntry()
    {
        var entry = AuditLog.Record(
            AuditAction.CaptureRegistered,
            captureId: CaptureId.New(),
            userId: "user-42",
            ipAddress: "10.0.0.1",
            reason: "unit test");
        entry.SetChainHashes("prev-hash", "content-hash");
        entry.SetServerSignature("server-sig");
        return entry;
    }

    [Fact]
    public async Task ArchiveAsync_CallsPutAsync_OnObjectStore()
    {
        var store = new InMemoryObjectStore();
        IAuditLogArchiveService sut = new ObjectStoreAuditLogArchiveService(store);
        var entry = MakeEntry();

        await sut.ArchiveAsync(entry);

        store.Count.Should().Be(1);
    }

    [Fact]
    public async Task ArchiveAsync_KeyMatchesDateBasedFormat()
    {
        var store = new InMemoryObjectStore();
        IAuditLogArchiveService sut = new ObjectStoreAuditLogArchiveService(store);
        var entry = MakeEntry();
        var before = DateTimeOffset.UtcNow;

        await sut.ArchiveAsync(entry);

        var key = store.Keys.Single();
        // Expected: audit/{yyyy}/{MM}/{dd}/{id}.json  (Gregorian, culture-invariant)
        key.Should().StartWith("audit/");
        key.Should().EndWith(".json");
        // Year must be the Gregorian year regardless of server locale.
        key.Should().Contain(before.UtcDateTime.Year.ToString());
    }

    [Fact]
    public async Task ArchiveAsync_StoredJsonContainsAction()
    {
        var store = new InMemoryObjectStore();
        IAuditLogArchiveService sut = new ObjectStoreAuditLogArchiveService(store);
        var entry = MakeEntry();

        await sut.ArchiveAsync(entry);

        var json = System.Text.Encoding.UTF8.GetString(store.Values.Single());
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("action").GetString()
            .Should().Be(nameof(AuditAction.CaptureRegistered));
    }

    [Fact]
    public async Task ArchiveAsync_StoredJsonContainsUserId()
    {
        var store = new InMemoryObjectStore();
        IAuditLogArchiveService sut = new ObjectStoreAuditLogArchiveService(store);
        var entry = MakeEntry();

        await sut.ArchiveAsync(entry);

        var json = System.Text.Encoding.UTF8.GetString(store.Values.Single());
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("userId").GetString().Should().Be("user-42");
    }

    [Fact]
    public async Task ArchiveAsync_StoredJsonContainsChainHashes()
    {
        var store = new InMemoryObjectStore();
        IAuditLogArchiveService sut = new ObjectStoreAuditLogArchiveService(store);
        var entry = MakeEntry();

        await sut.ArchiveAsync(entry);

        var json = System.Text.Encoding.UTF8.GetString(store.Values.Single());
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("previousHash").GetString().Should().Be("prev-hash");
        doc.RootElement.GetProperty("contentHash").GetString().Should().Be("content-hash");
        doc.RootElement.GetProperty("serverSignature").GetString().Should().Be("server-sig");
    }

    [Fact]
    public async Task ArchiveAsync_WhenStoreFails_DoesNotPropagate()
    {
        IAuditLogArchiveService sut = new ObjectStoreAuditLogArchiveService(new FailingObjectStore());

        var act = async () => await sut.ArchiveAsync(MakeEntry());

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NullAuditLogArchiveService_CompletesWithoutThrowing()
    {
        IAuditLogArchiveService sut = new NullAuditLogArchiveService();

        var act = async () => await sut.ArchiveAsync(MakeEntry());

        await act.Should().NotThrowAsync();
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    private sealed class FailingObjectStore : IObjectStore
    {
        public Task PutAsync(string key, byte[] data, CancellationToken ct) =>
            Task.FromException(new InvalidOperationException("archive unavailable"));
        public Task<byte[]> GetAsync(string key, CancellationToken ct) =>
            Task.FromResult(Array.Empty<byte>());
    }
}
