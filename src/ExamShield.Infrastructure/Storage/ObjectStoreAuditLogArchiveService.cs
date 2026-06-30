using ExamShield.Application.Interfaces;
using ExamShield.Domain.Entities;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace ExamShield.Infrastructure.Storage;

public sealed class ObjectStoreAuditLogArchiveService(
    IObjectStore store,
    ILogger<ObjectStoreAuditLogArchiveService>? logger = null) : IAuditLogArchiveService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented        = false,
    };

    public async Task ArchiveAsync(AuditLog entry, CancellationToken ct = default)
    {
        try
        {
            var key  = BuildKey(entry);
            var json = SerializeEntry(entry);
            await store.PutAsync(key, Encoding.UTF8.GetBytes(json), ct);
        }
        catch (Exception ex)
        {
            // Archive is a best-effort replica — failures must never block the primary audit write.
            logger?.LogError(ex,
                "Audit log archive failed for entry {AuditLogId}; the database entry was saved.",
                entry.Id);
        }
    }

    private static string BuildKey(AuditLog entry)
    {
        var d = entry.OccurredAt.UtcDateTime;
        return $"audit/{d.Year:D4}/{d.Month:D2}/{d.Day:D2}/{entry.Id}.json";
    }

    private static string SerializeEntry(AuditLog entry) =>
        JsonSerializer.Serialize(new
        {
            id              = entry.Id.ToString(),
            action          = entry.Action.ToString(),
            captureId       = entry.CaptureId?.ToString(),
            userId          = entry.UserId,
            ipAddress       = entry.IpAddress,
            occurredAt      = entry.OccurredAt,
            reason          = entry.Reason,
            previousHash    = entry.PreviousHash,
            contentHash     = entry.ContentHash,
            serverSignature = entry.ServerSignature,
        }, JsonOptions);
}
