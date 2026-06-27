using MediatR;

namespace ExamShield.Application.Queries.ExportAuditLog;

public sealed record ExportAuditLogResult(string Csv, string Filename);

public sealed record ExportAuditLogQuery(
    Guid? CaptureId,
    DateTimeOffset? From,
    DateTimeOffset? To) : IRequest<ExportAuditLogResult>;
