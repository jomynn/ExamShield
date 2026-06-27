using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Queries.GetAllActiveSessions;

public sealed record AllSessionDto(Guid Id, Guid UserId, DateTimeOffset CreatedAt, DateTimeOffset ExpiresAt);

public sealed record GetAllActiveSessionsResult(IReadOnlyList<AllSessionDto> Sessions);

public sealed record GetAllActiveSessionsQuery(Guid? UserId) : IRequest<GetAllActiveSessionsResult>;

public sealed class GetAllActiveSessionsQueryHandler(IRefreshTokenRepository tokens)
    : IRequestHandler<GetAllActiveSessionsQuery, GetAllActiveSessionsResult>
{
    public async Task<GetAllActiveSessionsResult> Handle(GetAllActiveSessionsQuery query, CancellationToken ct)
    {
        UserId? filter = query.UserId.HasValue ? new UserId(query.UserId.Value) : null;
        var active = await tokens.ListAllActiveAsync(filter, ct);
        var dtos = active
            .Select(t => new AllSessionDto(t.Id, t.UserId.Value, t.CreatedAt, t.ExpiresAt))
            .ToList();
        return new GetAllActiveSessionsResult(dtos);
    }
}
