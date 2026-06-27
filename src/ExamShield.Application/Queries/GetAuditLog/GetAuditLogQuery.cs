using MediatR;

namespace ExamShield.Application.Queries.GetAuditLog;

public sealed record GetAuditLogQuery(
    Guid? CaptureId = null,
    int Page = 1,
    int PageSize = 50,
    string? Action = null,
    string? UserId = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null
) : IRequest<GetAuditLogResult>;

public sealed record GetAuditLogResult(
    IReadOnlyList<AuditLogDto> Entries,
    int TotalCount
);

public sealed record AuditLogDto(
    Guid Id,
    string Action,
    Guid? CaptureId,
    string UserId,
    string IpAddress,
    DateTimeOffset OccurredAt,
    string? Reason,
    string ContentHash,
    string ServerSignature
);
