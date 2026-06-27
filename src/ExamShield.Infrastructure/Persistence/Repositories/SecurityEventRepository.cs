using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ExamShield.Infrastructure.Persistence.Repositories;

public sealed class SecurityEventRepository(ExamShieldDbContext context) : ISecurityEventRepository
{
    public async Task AddAsync(SecurityEvent securityEvent, CancellationToken ct = default)
    {
        await context.SecurityEvents.AddAsync(securityEvent, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<SecurityEvent>> ListRecentAsync(int limit, CancellationToken ct = default) =>
        await context.SecurityEvents
            .OrderByDescending(e => e.OccurredAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SecurityEvent>> ListBySeverityAsync(
        SecuritySeverity severity, int limit, CancellationToken ct = default) =>
        await context.SecurityEvents
            .Where(e => e.Severity == severity)
            .OrderByDescending(e => e.OccurredAt)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SecurityEvent>> ListByTypesAsync(
        IEnumerable<SecurityEventType> types, int limit,
        DateTimeOffset? from = null, DateTimeOffset? to = null,
        CancellationToken ct = default)
    {
        var set   = types.ToHashSet();
        var query = context.SecurityEvents.Where(e => set.Contains(e.EventType));
        if (from is not null) query = query.Where(e => e.OccurredAt >= from);
        if (to   is not null) query = query.Where(e => e.OccurredAt <= to);
        return await query
            .OrderByDescending(e => e.OccurredAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<SecurityEvent>> ListByCaptureIdAsync(
        Guid captureId, int limit, CancellationToken ct = default) =>
        await context.SecurityEvents
            .Where(e => e.CaptureId == captureId)
            .OrderByDescending(e => e.OccurredAt)
            .Take(limit)
            .ToListAsync(ct);

    public Task<int> CountAllAsync(CancellationToken ct = default) =>
        context.SecurityEvents.CountAsync(ct);

    public Task<int> CountBySeverityAsync(SecuritySeverity severity, CancellationToken ct = default) =>
        context.SecurityEvents.CountAsync(e => e.Severity == severity, ct);
}
