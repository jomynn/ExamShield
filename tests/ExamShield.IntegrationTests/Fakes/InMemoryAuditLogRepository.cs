using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.Services;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.IntegrationTests.Fakes;

public sealed class InMemoryAuditLogRepository(IServerSigningService signer) : IAuditLogRepository
{
    private readonly List<AuditLog> _entries = new();

    public Task AppendAsync(AuditLog entry, CancellationToken ct = default)
    {
        var previousHash = entry.CaptureId is not null
            ? _entries.LastOrDefault(e => e.CaptureId == entry.CaptureId)?.ContentHash ?? string.Empty
            : string.Empty;

        var contentHash = AuditChainHasher.ComputeContentHash(entry, previousHash);
        entry.SetChainHashes(previousHash, contentHash);
        entry.SetServerSignature(signer.Sign($"{contentHash}|{previousHash}"));

        _entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task<(IReadOnlyList<AuditLog> Entries, int TotalCount)> QueryAsync(
        CaptureId? captureId, int page, int pageSize,
        AuditAction? action = null,
        CancellationToken ct = default)
    {
        var query = _entries.AsEnumerable();
        if (captureId is not null) query = query.Where(e => e.CaptureId == captureId);
        if (action    is not null) query = query.Where(e => e.Action    == action);
        var total = query.Count();
        var entries = query
            .OrderByDescending(e => e.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();
        return Task.FromResult<(IReadOnlyList<AuditLog>, int)>((entries, total));
    }

    public Task<IReadOnlyList<AuditLog>> GetChainAsync(
        CaptureId captureId, CancellationToken ct = default)
    {
        var chain = _entries
            .Where(e => e.CaptureId == captureId)
            .OrderBy(e => e.OccurredAt)
            .ToList();
        return Task.FromResult<IReadOnlyList<AuditLog>>(chain);
    }

    public Task<IReadOnlyList<AuditLog>> ExportAsync(
        CaptureId? captureId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default)
    {
        var query = _entries.AsEnumerable();
        if (captureId is not null) query = query.Where(e => e.CaptureId == captureId);
        if (from is not null) query = query.Where(e => e.OccurredAt >= from.Value);
        if (to is not null) query = query.Where(e => e.OccurredAt <= to.Value);
        return Task.FromResult<IReadOnlyList<AuditLog>>(
            query.OrderBy(e => e.OccurredAt).ToList());
    }
}
