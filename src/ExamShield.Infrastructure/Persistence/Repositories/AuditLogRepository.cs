using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.Services;
using ExamShield.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace ExamShield.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository(ExamShieldDbContext context, IServerSigningService signer) : IAuditLogRepository
{
    public async Task AppendAsync(AuditLog entry, CancellationToken ct = default)
    {
        var previousHash = string.Empty;
        if (entry.CaptureId is not null)
        {
            var last = await context.AuditLogs
                .Where(e => e.CaptureId == entry.CaptureId)
                .OrderByDescending(e => e.OccurredAt)
                .FirstOrDefaultAsync(ct);
            previousHash = last?.ContentHash ?? string.Empty;
        }

        var contentHash = AuditChainHasher.ComputeContentHash(entry, previousHash);
        entry.SetChainHashes(previousHash, contentHash);
        entry.SetServerSignature(signer.Sign($"{contentHash}|{previousHash}"));

        await context.AuditLogs.AddAsync(entry, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task<(IReadOnlyList<AuditLog> Entries, int TotalCount)> QueryAsync(
        CaptureId? captureId, int page, int pageSize,
        AuditAction? action = null,
        CancellationToken ct = default)
    {
        var query = context.AuditLogs.AsQueryable();
        if (captureId is not null) query = query.Where(e => e.CaptureId == captureId);
        if (action    is not null) query = query.Where(e => e.Action    == action);
        var total = await query.CountAsync(ct);
        var entries = await query
            .OrderByDescending(e => e.OccurredAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);
        return (entries, total);
    }

    public async Task<IReadOnlyList<AuditLog>> GetChainAsync(
        CaptureId captureId, CancellationToken ct = default) =>
        await context.AuditLogs
            .Where(e => e.CaptureId == captureId)
            .OrderBy(e => e.OccurredAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AuditLog>> ExportAsync(
        CaptureId? captureId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default)
    {
        var query = context.AuditLogs.AsQueryable();
        if (captureId is not null) query = query.Where(e => e.CaptureId == captureId);
        if (from is not null) query = query.Where(e => e.OccurredAt >= from.Value);
        if (to is not null) query = query.Where(e => e.OccurredAt <= to.Value);
        return await query.OrderBy(e => e.OccurredAt).ToListAsync(ct);
    }
}
