using System.Text;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Queries.ExportAuditLog;

public sealed class ExportAuditLogQueryHandler(IAuditLogRepository repository)
    : IRequestHandler<ExportAuditLogQuery, ExportAuditLogResult>
{
    private static readonly string[] CsvHeader =
        ["Id", "Action", "CaptureId", "UserId", "IpAddress", "OccurredAt", "Reason", "ContentHash"];

    public async Task<ExportAuditLogResult> Handle(ExportAuditLogQuery query, CancellationToken ct)
    {
        var captureId = query.CaptureId.HasValue ? new CaptureId(query.CaptureId.Value) : null;
        var entries = await repository.ExportAsync(captureId, query.From, query.To, ct);

        var csv = new StringBuilder();
        csv.AppendLine(string.Join(",", CsvHeader));

        foreach (var e in entries)
        {
            csv.AppendLine(string.Join(",",
                e.Id.Value,
                e.Action,
                e.CaptureId?.Value.ToString() ?? "",
                EscapeCsv(e.UserId ?? ""),
                EscapeCsv(e.IpAddress ?? ""),
                e.OccurredAt.ToString("O"),
                EscapeCsv(e.Reason ?? ""),
                e.ContentHash));
        }

        var filename = $"audit-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.csv";
        return new ExportAuditLogResult(csv.ToString(), filename);
    }

    private static string EscapeCsv(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
}
