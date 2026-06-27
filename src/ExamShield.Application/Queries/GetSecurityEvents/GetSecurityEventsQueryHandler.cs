using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using MediatR;

namespace ExamShield.Application.Queries.GetSecurityEvents;

public sealed class GetSecurityEventsQueryHandler(ISecurityEventRepository repo)
    : IRequestHandler<GetSecurityEventsQuery, GetSecurityEventsResult>
{
    public async Task<GetSecurityEventsResult> Handle(GetSecurityEventsQuery request, CancellationToken ct)
    {
        IReadOnlyList<Domain.Entities.SecurityEvent> events;

        if (request.Severity is not null &&
            Enum.TryParse<SecuritySeverity>(request.Severity, ignoreCase: true, out var sev))
        {
            events = await repo.ListBySeverityAsync(sev, request.Limit, ct);
        }
        else
        {
            events = await repo.ListRecentAsync(request.Limit, ct);
        }

        var dtos = events.Select(e => new SecurityEventDto(
            e.Id, e.EventType.ToString(), e.Severity.ToString(),
            e.Message, e.UserId, e.IpAddress, e.CaptureId, e.OccurredAt
        )).ToList();
        return new GetSecurityEventsResult(dtos);
    }
}
