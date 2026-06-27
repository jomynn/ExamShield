using ExamShield.Domain.Enums;
using ExamShield.Domain.Interfaces;
using MediatR;

namespace ExamShield.Application.Queries.GetLoginHistory;

public sealed record GetLoginHistoryQuery(
    int Limit = 100,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null) : IRequest<GetLoginHistoryResult>;

public sealed record LoginHistoryDto(
    Guid Id, string EventType, string? UserId, string? IpAddress, DateTimeOffset OccurredAt);

public sealed record GetLoginHistoryResult(IReadOnlyList<LoginHistoryDto> Events);

public sealed class GetLoginHistoryQueryHandler(ISecurityEventRepository repo)
    : IRequestHandler<GetLoginHistoryQuery, GetLoginHistoryResult>
{
    private static readonly SecurityEventType[] LoginTypes =
        [SecurityEventType.LoginSuccess, SecurityEventType.LoginFailed];

    public async Task<GetLoginHistoryResult> Handle(GetLoginHistoryQuery request, CancellationToken ct)
    {
        var events = await repo.ListByTypesAsync(LoginTypes, request.Limit, request.From, request.To, ct);
        var dtos = events.Select(e =>
            new LoginHistoryDto(e.Id, e.EventType.ToString(), e.UserId, e.IpAddress, e.OccurredAt))
            .ToList();
        return new GetLoginHistoryResult(dtos);
    }
}
