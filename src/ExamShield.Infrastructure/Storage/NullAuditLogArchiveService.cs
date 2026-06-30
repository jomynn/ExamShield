using ExamShield.Application.Interfaces;
using ExamShield.Domain.Entities;

namespace ExamShield.Infrastructure.Storage;

// Used when no object storage is configured (e.g. InMemory test environment or AzureBlob).
public sealed class NullAuditLogArchiveService : IAuditLogArchiveService
{
    public Task ArchiveAsync(AuditLog entry, CancellationToken ct = default) => Task.CompletedTask;
}
