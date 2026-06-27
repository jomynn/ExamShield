using MediatR;

namespace ExamShield.Application.Commands.BatchScore;

public sealed record BatchScoreResult(int Scored, int Skipped);
public sealed record BatchScoreCommand(Guid ExamId) : IRequest<BatchScoreResult>;
