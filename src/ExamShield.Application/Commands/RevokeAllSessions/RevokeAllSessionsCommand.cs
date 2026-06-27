using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.RevokeAllSessions;

public sealed record RevokeAllSessionsCommand(Guid UserId) : IRequest;

public sealed class RevokeAllSessionsCommandHandler(IRefreshTokenRepository tokens)
    : IRequestHandler<RevokeAllSessionsCommand>
{
    public async Task Handle(RevokeAllSessionsCommand command, CancellationToken ct) =>
        await tokens.RevokeAllForUserAsync(new UserId(command.UserId), ct);
}
