using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.ValueObjects;

namespace ExamShield.Domain.Interfaces;

public interface IAuditLogRepository
{
    // Append-only: no UpdateAsync, no DeleteAsync
    Task AppendAsync(AuditLog entry, CancellationToken ct = default);

    Task<(IReadOnlyList<AuditLog> Entries, int TotalCount)> QueryAsync(
        CaptureId? captureId, int page, int pageSize,
        AuditAction? action = null,
        string? userId = null,
        DateTimeOffset? from = null,
        DateTimeOffset? to = null,
        CancellationToken ct = default);

    // Returns entries in ascending chronological order for chain traversal/verification.
    Task<IReadOnlyList<AuditLog>> GetChainAsync(CaptureId captureId, CancellationToken ct = default);

    // Returns all matching entries (no pagination) for export; respects optional filters.
    Task<IReadOnlyList<AuditLog>> ExportAsync(
        CaptureId? captureId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default);
}
