using ExamShield.Domain.Entities;

namespace ExamShield.Application.Interfaces;

public interface IAuditLogArchiveService
{
    Task ArchiveAsync(AuditLog entry, CancellationToken ct = default);
}
