using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;

namespace ExamShield.Domain.Interfaces;

public interface ISecurityEventRepository
{
    Task AddAsync(SecurityEvent securityEvent, CancellationToken ct = default);
    Task<IReadOnlyList<SecurityEvent>> ListRecentAsync(int limit, CancellationToken ct = default);
    Task<IReadOnlyList<SecurityEvent>> ListBySeverityAsync(SecuritySeverity severity, int limit, CancellationToken ct = default);
    Task<IReadOnlyList<SecurityEvent>> ListByTypesAsync(
        IEnumerable<SecurityEventType> types, int limit,
        DateTimeOffset? from = null, DateTimeOffset? to = null,
        CancellationToken ct = default);
    Task<int> CountAllAsync(CancellationToken ct = default);
    Task<int> CountBySeverityAsync(SecuritySeverity severity, CancellationToken ct = default);
}
