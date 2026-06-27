using MediatR;

namespace ExamShield.Application.Queries.GetAnswerKey;

public sealed record GetAnswerKeyResult(Guid ExamId, IReadOnlyDictionary<int, string> Answers, DateTimeOffset CreatedAt);
public sealed record GetAnswerKeyQuery(Guid ExamId) : IRequest<GetAnswerKeyResult>;
