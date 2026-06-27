using MediatR;

namespace ExamShield.Application.Queries.GetSecurityEvents;

public sealed record GetSecurityEventsQuery(
    int Limit = 100,
    string? Severity = null,
    Guid? CaptureId = null) : IRequest<GetSecurityEventsResult>;

public sealed record SecurityEventDto(
    Guid Id,
    string EventType,
    string Severity,
    string Message,
    string? UserId,
    string? IpAddress,
    Guid? CaptureId,
    DateTimeOffset OccurredAt
);

public sealed record GetSecurityEventsResult(IReadOnlyList<SecurityEventDto> Events);
