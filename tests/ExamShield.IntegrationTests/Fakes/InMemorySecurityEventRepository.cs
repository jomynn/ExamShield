using System.Collections.Concurrent;
using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;

namespace ExamShield.IntegrationTests.Fakes;

public sealed class InMemorySecurityEventRepository : ISecurityEventRepository
{
    private readonly ConcurrentBag<SecurityEvent> _store = new();

    public Task AddAsync(SecurityEvent securityEvent, CancellationToken ct = default)
    {
        _store.Add(securityEvent);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<SecurityEvent>> ListRecentAsync(int limit, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SecurityEvent>>(
            _store.OrderByDescending(e => e.OccurredAt).Take(limit).ToList());

    public Task<IReadOnlyList<SecurityEvent>> ListBySeverityAsync(
        SecuritySeverity severity, int limit, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<SecurityEvent>>(
            _store.Where(e => e.Severity == severity)
                  .OrderByDescending(e => e.OccurredAt).Take(limit).ToList());

    public Task<IReadOnlyList<SecurityEvent>> ListByTypesAsync(
        IEnumerable<SecurityEventType> types, int limit, CancellationToken ct = default)
    {
        var set = types.ToHashSet();
        return Task.FromResult<IReadOnlyList<SecurityEvent>>(
            _store.Where(e => set.Contains(e.EventType))
                  .OrderByDescending(e => e.OccurredAt).Take(limit).ToList());
    }

    public Task<int> CountAllAsync(CancellationToken ct = default) =>
        Task.FromResult(_store.Count);

    public Task<int> CountBySeverityAsync(SecuritySeverity severity, CancellationToken ct = default) =>
        Task.FromResult(_store.Count(e => e.Severity == severity));
}
