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
        IEnumerable<SecurityEventType> types, int limit, CancellationToken ct = default)
    {
        var set = types.ToHashSet();
        return await context.SecurityEvents
            .Where(e => set.Contains(e.EventType))
            .OrderByDescending(e => e.OccurredAt)
            .Take(limit)
            .ToListAsync(ct);
    }

    public Task<int> CountAllAsync(CancellationToken ct = default) =>
        context.SecurityEvents.CountAsync(ct);

    public Task<int> CountBySeverityAsync(SecuritySeverity severity, CancellationToken ct = default) =>
        context.SecurityEvents.CountAsync(e => e.Severity == severity, ct);
}
